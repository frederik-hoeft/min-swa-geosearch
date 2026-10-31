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
    }
}
