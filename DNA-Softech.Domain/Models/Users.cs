using System.ComponentModel.DataAnnotations;

namespace DNASoftech.Domain.Models
{
    public class Users
    {
        [Key]
        public int UserId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Email must contain @ and be a valid format")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email must contain @ and be in valid format (example@domain.com)")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\+?[0-9]{1,3}[0-9]{9,10}$|^\+91[0-9]{10}$|^[0-9]{10}$", 
            ErrorMessage = "Phone number must be valid (e.g., +91 followed by 10 digits, or 10 digit number)")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Company name is required")]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; }

        [Required(ErrorMessage = "Consultation date is required")]
        public DateTime AppointmentDate { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "Project description is required")]
        [Display(Name = "Project Description")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string ProjectDescription { get; set; }
    }
}

