using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace GeoSearch.Web.Server.Data;

public sealed partial class GeoSearchUser : IDiscoverableModelConfiguration<GeoSearchUser>
{
    public static void Configure(EntityTypeBuilder<GeoSearchUser> self)
    {
        ArgumentNullException.ThrowIfNull(self);
        self.ToTable("AspNetUsers");

        // Identity's inherited properties are explicitly named to comply with the same WKG
        // naming/mapping policies as the other application-owned entities.
        self.Property(static user => user.Id).HasColumnName("Id").HasColumnType("text");
        self.Property(static user => user.UserName).HasColumnName("UserName").HasColumnType("character varying(256)");
        self.Property(static user => user.NormalizedUserName).HasColumnName("NormalizedUserName").HasColumnType("character varying(256)");
        self.Property(static user => user.Email).HasColumnName("Email").HasColumnType("character varying(256)");
        self.Property(static user => user.NormalizedEmail).HasColumnName("NormalizedEmail").HasColumnType("character varying(256)");
        self.Property(static user => user.EmailConfirmed).HasColumnName("EmailConfirmed").HasColumnType("boolean");
        self.Property(static user => user.PasswordHash).HasColumnName("PasswordHash").HasColumnType("text");
        self.Property(static user => user.SecurityStamp).HasColumnName("SecurityStamp").HasColumnType("text");
        self.Property(static user => user.ConcurrencyStamp).HasColumnName("ConcurrencyStamp").HasColumnType("text");
        self.Property(static user => user.PhoneNumber).HasColumnName("PhoneNumber").HasColumnType("text");
        self.Property(static user => user.PhoneNumberConfirmed).HasColumnName("PhoneNumberConfirmed").HasColumnType("boolean");
        self.Property(static user => user.TwoFactorEnabled).HasColumnName("TwoFactorEnabled").HasColumnType("boolean");
        self.Property(static user => user.LockoutEnd).HasColumnName("LockoutEnd").HasColumnType("timestamp with time zone");
        self.Property(static user => user.LockoutEnabled).HasColumnName("LockoutEnabled").HasColumnType("boolean");
        self.Property(static user => user.AccessFailedCount).HasColumnName("AccessFailedCount").HasColumnType("integer");
    }
}
