using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace GeoSearch.Web.Server.Data.RefreshTokens;

internal sealed partial class RefreshToken : IDiscoverableModelConfiguration<RefreshToken>
{
    public static void Configure(EntityTypeBuilder<RefreshToken> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        self.ToTable("RefreshTokens");
        self.HasKey(static token => token.Id);

        self.Property(static token => token.Id)
            .HasColumnName("Id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();
        self.Property(static token => token.UserId)
            .HasColumnName("UserId")
            .HasColumnType("text")
            .IsRequired();
        self.Property(static token => token.TokenHash)
            .HasColumnName("TokenHash")
            .HasColumnType("character varying(64)")
            .HasMaxLength(64)
            .IsRequired();
        self.Property(static token => token.FamilyId)
            .HasColumnName("FamilyId")
            .HasColumnType("uuid")
            .IsRequired();
        self.Property(static token => token.SecurityStamp)
            .HasColumnName("SecurityStamp")
            .HasColumnType("text");
        self.Property(static token => token.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        self.Property(static token => token.ExpiresAt)
            .HasColumnName("ExpiresAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        self.Property(static token => token.RevokedAt)
            .HasColumnName("RevokedAt")
            .HasColumnType("timestamp with time zone");
        self.Property(static token => token.ReplacedByTokenHash)
            .HasColumnName("ReplacedByTokenHash")
            .HasColumnType("character varying(64)")
            .HasMaxLength(64);
        self.Property(static token => token.ConcurrencyToken)
            .HasColumnName("ConcurrencyToken")
            .HasColumnType("character varying(32)")
            .HasMaxLength(32)
            .IsConcurrencyToken()
            .IsRequired();

        self.HasIndex(static token => token.TokenHash).IsUnique();
        self.HasIndex(static token => token.FamilyId);
        self.HasIndex(static token => token.ExpiresAt);

        self.HasOne(static token => token.User)
            .WithMany()
            .HasForeignKey(static token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
