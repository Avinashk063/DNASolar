namespace DNASoftech.Application.DTOs.Email
{
    public class EmailMessageDto
    {
        public IReadOnlyCollection<string> To { get; set; } = Array.Empty<string>();
        public string Subject { get; set; } = string.Empty;
        public string HtmlBody { get; set; } = string.Empty;
    }
}

