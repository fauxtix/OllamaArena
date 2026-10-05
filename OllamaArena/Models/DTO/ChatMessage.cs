namespace OllamaArena.Models.DTO
{
    public class ChatMessage
    {
        /// <summary>
        /// Texto marcador enquanto o modelo ainda não devolveu nada. Antes era a string
        /// mágica "..." replicada em 6 sítios (chat, composer, formatter); é preciso porque
        /// o <see cref="MessageFormatter"/> transforma este texto nos "typing dots" e o
        /// histórico enviado ao modelo tem de a omitir.
        /// </summary>
        public const string PendingPlaceholder = "...";

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string User { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsCurrentUser { get; set; }
        public string? ElapsedTime { get; set; }
        public double Temperature { get; set; }
        public string? Reasoning { get; set; }
        public string? Summary { get; set; }
        public string? LanguageWarning { get; set; }

        /// <summary>
        /// Verdadeiro enquanto a mensagem representa apenas o marcador "a aguardar o modelo"
        /// (primeiro render, antes do primeiro token). Evita comparar strings soltas por
        /// vários sítios e comparações que disparam o aviso de parse errado.
        /// </summary>
        public bool IsPending => string.Equals(Text, PendingPlaceholder, StringComparison.Ordinal);

        /// <summary>
        /// Verdadeiro quando a mensagem ainda não pode ser persistida (sem texto real, ou
        /// ainda a transmitir). Impede gravar turnos vazios/órfãos na base de dados.
        /// </summary>
        public bool HasPersistableContent => !IsPending && !string.IsNullOrWhiteSpace(Text);
    }
}
