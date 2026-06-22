using System;

namespace MyFirstCRM
{
    public class Message
    {
        public int Id { get; set; }
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsSent { get; set; }
        public string? ErrorMessage { get; set; }
        
        public Message()
        {
            SentAt = DateTime.Now;
            IsSent = false;
        }
        
        public Message(string toEmail, string subject, string body) : this()
        {
            ToEmail = toEmail;
            Subject = subject;
            Body = body;
        }
    }
}
