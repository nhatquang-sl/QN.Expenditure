namespace Domain.Entities
{
    public class UserLoginHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
