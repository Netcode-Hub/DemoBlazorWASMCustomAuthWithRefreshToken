using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoBlazorWASMCustomAuthWithRefreshToken.Shared.Models
{
    public class RegisterModel
    {
        public int Id { get; set; }
        [Required]
        public string? Name { get; set; }
        [Required, DataType(DataType.EmailAddress), EmailAddress]
        public string? Email { get; set; }
        [Required, DataType(DataType.Password)]
        public string? Password { get; set; }
        [Required, Compare("Password"), DataType(DataType.Password)]
        public string? ConfirmPassword { get; set; }
    }
}
