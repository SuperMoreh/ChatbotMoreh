using Microsoft.AspNetCore.Mvc;
using Twilio.TwiML;
using Twilio.TwiML.Messaging;
using System.Collections.Concurrent;

namespace ChatbotCobranzaMovil.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatbotController : ControllerBase
    {
        private static ConcurrentDictionary<string, Conversacion> conversaciones = new();

        [HttpPost]
        public IActionResult RecibirMensaje([FromForm] string From, [FromForm] string Body)
        {
            var respuesta = new MessagingResponse();

            var telefono = From;
            var mensaje = Body?.Trim().ToLower() ?? "";

            if (!conversaciones.ContainsKey(telefono))
                conversaciones[telefono] = new Conversacion();

            var estado = conversaciones[telefono];

            if ((DateTime.Now - estado.UltimoMensaje).TotalSeconds > 120)
            {
                conversaciones[telefono] = new Conversacion();
                estado = conversaciones[telefono];
                respuesta.Message("⌛ Tu sesión anterior ha expirado por inactividad. Escribe 'Hola' para comenzar de nuevo.");
                return Content(respuesta.ToString(), "application/xml");
            }

            estado.UltimoMensaje = DateTime.Now;

            switch (estado.Paso)
            {
                case 0:
                    respuesta.Message("¡Hola! Ingresa tu número de ruta:");
                    estado.Paso = 1;
                    break;

                case 1:
                    estado.Ruta = mensaje.Replace("ruta", "").Trim();
                    if (!string.IsNullOrEmpty(estado.Ruta))
                    {
                        respuesta.Message("Si necesitas permiso para *generar recibo de sucursal* escribe 1, o si necesitas permiso para *cancelación de recibo*, escribe 2:");
                        estado.Paso = 2;
                    }
                    else
                    {
                        respuesta.Message("Por favor ingresa un número de ruta válido.");
                    }
                    break;

                case 2:
                    if (mensaje == "1")
                    {
                        estado.TipoPermiso = "reimpresion";
                        respuesta.Message("Explica el motivo de tu solicitud:");
                        estado.Paso = 3;
                    }
                    else if (mensaje == "2")
                    {
                        estado.TipoPermiso = "cancelacion";
                        respuesta.Message("Explica el motivo de tu solicitud:");
                        estado.Paso = 3;
                    }
                    else
                    {
                        respuesta.Message("Por favor, escribe 1 para *generar recibo de sucursal* o 2 para *cancelación*.");
                    }
                    break;

                case 3:
                    estado.Motivo = mensaje;

                    try
                    {
                        var firebase = new ccFirebase20();
                        string ruta = estado.Ruta;
                        string tipo = estado.TipoPermiso == "reimpresion" ? "reimpresiones" : "cancelaciones";
                        string id = Guid.NewGuid().ToString();

                        var data = new
                        {
                            tipoPermiso = estado.TipoPermiso,
                            fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            motivo = estado.Motivo
                        };

                        var infoResponse = firebase.client.Set($"InfoPermisos/{ruta}/{id}", data);
                        var permisoResponse = firebase.client.Set($"Permisos/{ruta}/{tipo}", "1");

                        if (infoResponse.StatusCode.ToString() == "OK" && permisoResponse.StatusCode.ToString() == "OK")
                        {
                            respuesta.Message("✅ Permiso otorgado exitosamente. ¡Hasta luego!");
                        }
                        else
                        {
                            respuesta.Message("❌ Ocurrió un problema al otorgar el permiso. Intenta nuevamente.");
                        }
                    }
                    catch (Exception ex)
                    {
                        respuesta.Message("⚠️ Ocurrió un error al conectar con Firebase.");
                        Console.WriteLine("Firebase Error: " + ex.Message);
                    }

                    conversaciones.TryRemove(telefono, out _);
                    break;

                default:
                    respuesta.Message("Algo salió mal. Escribe 'Hola' para empezar de nuevo.");
                    conversaciones.TryRemove(telefono, out _);
                    break;
            }

            return Content(respuesta.ToString(), "application/xml");
        }
        [HttpGet("keepalive")]
        public IActionResult KeepAlive()
        {
            return Ok("Chatbot activo");
        }


        public class Conversacion
        {
            public int Paso { get; set; } = 0;
            public string Ruta { get; set; } = "";
            public string TipoPermiso { get; set; } = "";
            public string Motivo { get; set; } = "";
            public DateTime UltimoMensaje { get; set; } = DateTime.Now;
        }
    }
}
