using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMM.Api
{
    public class Message
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string? Direction { get; set; }
        public string? MessageName { get; set; }
        public string? MessageSchema { get; set; }
        public string? MessageDescription { get; set; }
        public string? ModuleName { get; set; }
        public string? AssemblyName { get; set; }
        public string? CompilationId { get; set; }
        public string? Location { get; set; }
        public DateTimeOffset? CreationDate { get; set; }
    }
}
