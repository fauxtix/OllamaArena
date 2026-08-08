namespace OllamaFluentUIChat.Services.Exceptions
{
    public class QuotaLimitException : Exception
    {
        public int SegundosRestantes { get; }
        public QuotaLimitException(int segundosRestantes)
            : base($"Por favor, aguarde {segundosRestantes} segundos antes de enviar uma nova avaliação.")
        {
            SegundosRestantes = segundosRestantes;
        }
    }
}
