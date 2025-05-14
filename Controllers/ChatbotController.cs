using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Collections.Concurrent;

namespace ChatbotCobranzaMovil.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramBotController : ControllerBase
    {
        private static readonly string token = "7740904178:AAFHYO_kl-xcWT46vFuQDCvYyf7gnHgXbeY";
        private static readonly string apiUrl = $"https://api.telegram.org/bot{token}/sendMessage";
        private static ConcurrentDictionary<string, Conversacion> conversaciones = new();

        [HttpPost("update")]
        public async Task<IActionResult> RecibirMensaje()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var rawBody = await reader.ReadToEndAsync();

                Console.WriteLine("📥 JSON recibido:");
                Console.WriteLine(rawBody); // Aquí verás el JSON real en los logs

                var update = JsonConvert.DeserializeObject<TelegramUpdate>(rawBody);

                if (update?.message?.text == null)
                    return Ok();

                string chatId = update.message.chat.id.ToString();
                string mensaje = update.message.text.Trim().ToLower();

                if (!conversaciones.ContainsKey(chatId))
                    conversaciones[chatId] = new Conversacion();

                var estado = conversaciones[chatId];

                if ((DateTime.Now - estado.UltimoMensaje).TotalSeconds > 120)
                {
                    conversaciones[chatId] = new Conversacion();
                    estado = conversaciones[chatId];
                    await EnviarMensaje(chatId, "⌛ Tu sesión anterior ha expirado por inactividad. Escribe 'Hola' para comenzar de nuevo.");
                    return Ok();
                }

                estado.UltimoMensaje = DateTime.Now;

                switch (estado.Paso)
                {
                    case 0:
                        await EnviarMensaje(chatId, "¡Hola! Ingresa tu número de ruta:");
                        estado.Paso = 1;
                        break;

                    case 1:
                        estado.Ruta = mensaje.Replace("ruta", "").Trim();
                        if (!string.IsNullOrEmpty(estado.Ruta))
                        {
                            await EnviarMensaje(chatId, "Escribe 1 para *generar recibo de sucursal* o 2 para *cancelación de recibo*:");
                            estado.Paso = 2;
                        }
                        else
                        {
                            await EnviarMensaje(chatId, "Por favor ingresa un número de ruta válido.");
                        }
                        break;

                    case 2:
                        if (mensaje == "1")
                        {
                            estado.TipoPermiso = "reimpresion";
                            await EnviarMensaje(chatId, "Explica el motivo de tu solicitud:");
                            estado.Paso = 3;
                        }
                        else if (mensaje == "2")
                        {
                            estado.TipoPermiso = "cancelacion";
                            await EnviarMensaje(chatId, "Explica el motivo de tu solicitud:");
                            estado.Paso = 3;
                        }
                        else
                        {
                            await EnviarMensaje(chatId, "Por favor, escribe 1 para *reimpresión* o 2 para *cancelación*.");
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
                                await EnviarMensaje(chatId, "✅ Permiso otorgado exitosamente. ¡Hasta luego!");
                            else
                                await EnviarMensaje(chatId, "❌ Ocurrió un problema al otorgar el permiso. Intenta nuevamente.");
                        }
                        catch (Exception ex)
                        {
                            await EnviarMensaje(chatId, "⚠️ Error al conectar con Firebase.");
                            Console.WriteLine("Firebase Error: " + ex.Message);
                        }

                        conversaciones.TryRemove(chatId, out _);
                        break;

                    default:
                        await EnviarMensaje(chatId, "Algo salió mal. Escribe 'Hola' para empezar de nuevo.");
                        conversaciones.TryRemove(chatId, out _);
                        break;
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error general: " + ex.Message);
                return Ok();
            }
        }


        private async Task EnviarMensaje(string chatId, string texto)
        {
            using var client = new HttpClient();
            var payload = new
            {
                chat_id = chatId,
                text = texto,
                parse_mode = "Markdown"
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            await client.PostAsync(apiUrl, content);
        }

        public class Conversacion
        {
            public int Paso { get; set; } = 0;
            public string Ruta { get; set; } = "";
            public string TipoPermiso { get; set; } = "";
            public string Motivo { get; set; } = "";
            public DateTime UltimoMensaje { get; set; } = DateTime.Now;
        }

        public class TelegramUpdate
        {
            [JsonProperty("message")]
            public TelegramMessage message { get; set; }
        }

        public class TelegramMessage
        {
            [JsonProperty("message_id")]
            public long message_id { get; set; }

            [JsonProperty("from")]
            public TelegramUser from { get; set; }

            [JsonProperty("chat")]
            public TelegramChat chat { get; set; }

            [JsonProperty("date")]
            public int date { get; set; }

            [JsonProperty("text")]
            public string text { get; set; }
        }

        public class TelegramUser
        {
            [JsonProperty("id")]
            public long id { get; set; }

            [JsonProperty("is_bot")]
            public bool is_bot { get; set; }

            [JsonProperty("first_name")]
            public string first_name { get; set; }

            [JsonProperty("username")]
            public string username { get; set; }
        }

        public class TelegramChat
        {
            [JsonProperty("id")]
            public long id { get; set; }

            [JsonProperty("first_name")]
            public string first_name { get; set; }

            [JsonProperty("username")]
            public string username { get; set; }

            [JsonProperty("type")]
            public string type { get; set; }
        }
    }
}
