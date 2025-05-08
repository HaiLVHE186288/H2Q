namespace HaiLV_H2Q.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class LoginLog
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime LoginTime { get; set; }
        public string? IpAddress { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string? Email { get; set; }
    }

    public class UpdateUserRequest
    {
        public string Username { get; set; }
        public string? Email { get; set; }
    }
}