using HaiLV_H2Q.Data;
using HaiLV_H2Q.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HaiLV_H2Q.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly DBM _dbm;
        private readonly IConfiguration _configuration;

        public UsersController(DBM dbm, IConfiguration configuration)
        {
            _dbm = dbm ?? throw new ArgumentNullException(nameof(dbm));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                return BadRequest("Invalid login request");

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "p_Username", request.Username }
                };
                var dt = _dbm.ExecuteStoredProcedure("sp_GetUserByUsername", parameters);

                // Debug: Log schema của DataTable
                Console.WriteLine("DataTable Columns: " + string.Join(", ", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));

                if (dt.Rows.Count == 0)
                    return Unauthorized("Invalid credentials");

                var user = Convertor.ToList<User>(dt, row => new User
                {
                    Id = dt.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    Username = dt.Columns.Contains("Username") ? row.Field<string>("Username")! : string.Empty,
                    Password = dt.Columns.Contains("Password") ? row.Field<string>("Password")! : string.Empty,
                    Email = dt.Columns.Contains("Email") ? row.Field<string?>("Email") : null,
                    CreatedAt = dt.Columns.Contains("CreatedAt") ? row.Field<DateTime?>("CreatedAt") ?? DateTime.Now : DateTime.Now
                }).FirstOrDefault();

                // TODO: Thêm mã hóa mật khẩu (như BCrypt) trước khi triển khai sản xuất
                if (user == null || user.Id == 0 || user.Password != request.Password)
                    return Unauthorized("Invalid credentials");

                // Log login attempt
                try
                {
                    _dbm.ExecuteNonQuery("sp_InsertLoginLog", new Dictionary<string, object>
                    {
                        { "p_UserId", user.Id },
                        { "p_IpAddress", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown" }
                    });
                }
                catch (Exception logEx)
                {
                    Console.WriteLine($"InsertLoginLog error: {logEx.Message}\nStackTrace: {logEx.StackTrace}");
                    // Tiếp tục dù lỗi ghi log
                }

                // Generate JWT
                var token = GenerateJwtToken(user);
                return Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost]
        [Authorize]
        public IActionResult CreateUser([FromBody] CreateUserRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                return BadRequest("Invalid create user request");

            try
            {
                // Debug: Log thông tin người dùng mới
                Console.WriteLine($"Creating user: {request.Username}, Email: {request.Email}");

                var parameters = new Dictionary<string, object>
                {
                    { "p_Username", request.Username },
                    { "p_Password", request.Password },
                    { "p_Email", request.Email ?? (object)DBNull.Value }
                };
                var dt = _dbm.ExecuteStoredProcedure("sp_CreateUser", parameters);

                // Debug: Log thông tin DataTable
                Console.WriteLine($"CreateUser DataTable: Rows={dt.Rows.Count}, Columns={string.Join(", ", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}");
                if (dt.Rows.Count == 0 || !dt.Columns.Contains("Id"))
                {
                    Console.WriteLine("CreateUser error: No ID returned from sp_CreateUser");
                    return StatusCode(500, "Failed to create user: No ID returned from database");
                }

                var idValue = dt.Rows[0].Field<ulong>("Id");
                if (idValue > int.MaxValue)
                {
                    Console.WriteLine($"CreateUser error: ID value {idValue} exceeds Int32 range");
                    return StatusCode(500, "Failed to create user: ID value exceeds maximum allowed value");
                }

                var id = (int)idValue;
                if (id == 0)
                {
                    Console.WriteLine("CreateUser error: Invalid ID (0) returned from sp_CreateUser");
                    return StatusCode(500, "Failed to create user: Invalid ID returned from database");
                }

                return Ok(new { Id = id });
            }
            catch (MySql.Data.MySqlClient.MySqlException ex) when (ex.Number == 1062)
            {
                Console.WriteLine($"Create user error: Duplicate username '{request.Username}'\nStackTrace: {ex.StackTrace}");
                return Conflict($"Username '{request.Username}' already exists");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create user error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public IActionResult UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Username))
                return BadRequest("Invalid update user request");

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "p_Id", id },
                    { "p_Username", request.Username },
                    { "p_Email", request.Email ?? (object)DBNull.Value }
                };
                _dbm.ExecuteNonQuery("sp_UpdateUser", parameters);
                return Ok();
            }
            catch (MySql.Data.MySqlClient.MySqlException ex) when (ex.Number == 1062)
            {
                Console.WriteLine($"Update user error: Duplicate username '{request.Username}'\nStackTrace: {ex.StackTrace}");
                return Conflict($"Username '{request.Username}' already exists");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update user error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "p_Id", id }
                };
                _dbm.ExecuteNonQuery("sp_DeleteUser", parameters);
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete user error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "p_PageNumber", pageNumber },
                    { "p_PageSize", pageSize }
                };
                var dt = _dbm.ExecuteStoredProcedure("sp_GetUsersPaged", parameters);

                // Debug: Log schema của DataTable
                Console.WriteLine("DataTable Columns: " + string.Join(", ", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));

                var users = Convertor.ToList<UserDto>(dt, row => new UserDto
                {
                    Id = dt.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    Username = dt.Columns.Contains("Username") ? row.Field<string>("Username")! : string.Empty,
                    Email = dt.Columns.Contains("Email") ? row.Field<string?>("Email") : null,
                    CreatedAt = dt.Columns.Contains("CreatedAt") ? row.Field<DateTime?>("CreatedAt") ?? DateTime.Now : DateTime.Now
                });
                return Ok(users);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get users error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private string GenerateJwtToken(User user)
        {
            try
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username)
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    claims: claims,
                    expires: DateTime.Now.AddHours(1),
                    signingCredentials: creds);

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GenerateJwtToken error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw;
            }
        }
    }
}