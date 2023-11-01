using Microsoft.EntityFrameworkCore;

namespace MMM.Api
{
    public class MessagesDbContext : DbContext
    {
        public MessagesDbContext(DbContextOptions<MessagesDbContext> options) : base(options) { }
        public  DbSet<Message> Messages { get; set; }
    }
}
