using System.ComponentModel.DataAnnotations;

namespace DNASoftech.Domain.Models.ECommerce
{
    /// <summary>
    /// Application-level authenticated user for the e-commerce shop.
    /// Separate from the Domain <see cref="DNASoftech.Domain.Models.Users"/> entity which
    /// represents appointment/consultation contacts.
    /// </summary>
    public class AppUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        /// <summary>SHA-256 hex hash of the user's password.</summary>
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>Simple role string: "User" | "Admin"</summary>
        [MaxLength(50)]
        public string Role { get; set; } = "User";

        public string? Mobile { get; set; }

        // Legacy combined address string kept for backwards compat
        public string? Address { get; set; }

        // Structured address fields
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? Country { get; set; }

        // Profile image stored as binary in DB (small avatar)
        public byte[]? ProfileImageData { get; set; }
        public string? ProfileImageMimeType { get; set; }
    }
}
