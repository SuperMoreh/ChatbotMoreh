namespace ChatbotCobranzaMovil
{
    public class ChatbotConversacion
    {
        public enum EstadoConversacion
        {
            EsperandoRuta,
            EsperandoTipoPermiso,
            EsperandoMotivo,
            Finalizado
        }

        public string Ruta { get; set; }
        public string TipoPermiso { get; set; } 
        public string Motivo { get; set; }
        public EstadoConversacion EstadoActual { get; set; } = EstadoConversacion.EsperandoRuta;
    }

}
