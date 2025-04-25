using FireSharp.Config;
using FireSharp.Interfaces;
using System.IO;

namespace ChatbotCobranzaMovil
{
    public class ccFirebase20
    {
        public readonly IFirebaseClient client;

        public ccFirebase20()
        {
            var config = new FirebaseConfig
            {
                AuthSecret = "NGCahFpAuJg7xMF4PRVwN5XzTr0vcrBZVfwgOT2Q",
                BasePath = "https://cobranzadigital2-a5ab6-default-rtdb.firebaseio.com/"
            };

            client = new FireSharp.FirebaseClient(config);
        }

        public async Task OtorgarPermiso(string ruta, string tipo)
        {
            string campo = tipo == "1" ? "reimpresiones" : "cancelaciones";
            var data = new Dictionary<string, string>
        {
            { campo, "1" }
        };
            await client.UpdateAsync($"Permisos/{ruta}", data);
        }
    }
}
