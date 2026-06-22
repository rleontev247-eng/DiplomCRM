using System.Text.Json.Serialization;
using MyFirstCRM;

namespace MyFirstCRM.Supabase
{
    // Модели для синхронизации с Supabase с правильными именами колонок
    
    public class SupabaseClient
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("companyid")]
        public int CompanyId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("phone")]
        public string? Phone { get; set; }
        [JsonPropertyName("email")]
        public string? Email { get; set; }
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("abc_category")]
        public string? ABC_Category { get; set; }
    }

    public class SupabaseDeal
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("clientid")]
        public int ClientId { get; set; }
        [JsonPropertyName("companyid")]
        public int CompanyId { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; } = "new";
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    public class SupabaseExpense
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("companyid")]
        public int CompanyId { get; set; }
        [JsonPropertyName("clientid")]
        public int? ClientId { get; set; }
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    public class SupabaseNotification
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("companyid")]
        public int CompanyId { get; set; }
        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; } = "info";
        [JsonPropertyName("is_read")]
        public bool IsRead { get; set; } = false;
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("clientid")]
        public int? ClientId { get; set; }
    }

    public class SupabaseCalendarEvent
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("companyid")]
        public int CompanyId { get; set; }
        [JsonPropertyName("clientid")]
        public int? ClientId { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("start_time")]
        public DateTime StartTime { get; set; }
        [JsonPropertyName("end_time")]
        public DateTime? EndTime { get; set; }
        [JsonPropertyName("location")]
        public string? Location { get; set; }
        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = "meeting";
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
        [JsonPropertyName("assignedtouser")]
        public int AssignedToUserId { get; set; }
    }
}
