using HR28.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace HR28.Infrastructure.Data;

public class HR28DbContext : DbContext
{
    public HR28DbContext(
        DbContextOptions<HR28DbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --------------------------------------------------
        // User and role relationships
        // --------------------------------------------------

        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new
            {
                ur.UserId,
                ur.RoleId
            });

        // What opens when someone with the role opens a voter ("Full" / "AddEncounter").
        modelBuilder.Entity<Role>()
            .Property(r => r.VoterProfileView)
            .HasMaxLength(20)
            .HasDefaultValue("Full");

        // Where someone with the role lands after signing in ("Dashboard" / "QuickEntry").
        modelBuilder.Entity<Role>()
            .Property(r => r.StartPage)
            .HasMaxLength(20)
            .HasDefaultValue("Dashboard");

        // "Remember me on this device": only a keyed hash of the device key is stored.
        modelBuilder.Entity<TrustedDevice>(e =>
        {
            e.Property(d => d.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(d => d.TokenHash).IsUnique();
            e.Property(d => d.Name).HasMaxLength(100);
            e.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // --------------------------------------------------
        // User scope relationships
        // --------------------------------------------------

        modelBuilder.Entity<UserScope>()
            .HasKey(us => us.Id);

        modelBuilder.Entity<UserScope>()
            .HasOne(us => us.User)
            .WithMany(u => u.UserScopes)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserScope>()
            .HasOne(us => us.Constituency)
            .WithMany(c => c.UserScopes)
            .HasForeignKey(us => us.ConstituencyId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserScope>()
            .HasOne(us => us.Island)
            .WithMany(i => i.UserScopes)
            .HasForeignKey(us => us.IslandId)
            .OnDelete(DeleteBehavior.NoAction);

        // --------------------------------------------------
        // Constituency and island many-to-many relationship
        // --------------------------------------------------

        modelBuilder.Entity<ConstituencyIsland>()
            .HasKey(ci => new
            {
                ci.ConstituencyId,
                ci.IslandId
            });

        modelBuilder.Entity<ConstituencyIsland>()
            .HasOne(ci => ci.Constituency)
            .WithMany(c => c.ConstituencyIslands)
            .HasForeignKey(ci => ci.ConstituencyId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ConstituencyIsland>()
            .HasOne(ci => ci.Island)
            .WithMany(i => i.ConstituencyIslands)
            .HasForeignKey(ci => ci.IslandId)
            .OnDelete(DeleteBehavior.NoAction);

        /*
         * Transitional configuration:
         *
         * Island.ConstituencyId remains in the entity for now.
         * EF Core will continue configuring that existing
         * one-to-many relationship by convention.
         *
         * Do not remove ConstituencyId from Island.cs until:
         * 1. ConstituencyIslands has been populated.
         * 2. Scope queries use ConstituencyIslands.
         * 3. Existing records have been verified.
         */

        // --------------------------------------------------
        // Encounter relationships
        // --------------------------------------------------

        modelBuilder.Entity<Encounter>()
            .HasOne(e => e.Voter)
            .WithMany(v => v.Encounters)
            .HasForeignKey(e => e.VoterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Encounter>()
            .Property(e => e.Response)
            .HasMaxLength(20);

        modelBuilder.Entity<Encounter>()
            .HasOne(e => e.RecordedByUser)
            .WithMany(u => u.RecordedEncounters)
            .HasForeignKey(e => e.RecordedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // --------------------------------------------------
        // Pledge relationships
        // --------------------------------------------------

        modelBuilder.Entity<Pledge>()
            .HasOne(p => p.Voter)
            .WithMany(v => v.Pledges)
            .HasForeignKey(p => p.VoterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Pledge>()
            .HasOne(p => p.CreatedByUser)
            .WithMany(u => u.CreatedPledges)
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Pledge>()
            .HasOne(p => p.AssignedToUser)
            .WithMany(u => u.AssignedPledges)
            .HasForeignKey(p => p.AssignedToUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // --------------------------------------------------
        // Voter and influencer relationships
        // --------------------------------------------------

        // ------------------------------------------------
        // Voter and influencer relationships
        // ------------------------------------------------

        modelBuilder.Entity<VoterInfluencer>()
            .HasOne(vi => vi.Voter)
            .WithMany(v => v.Influencers)
            .HasForeignKey(vi => vi.VoterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VoterInfluencer>()
            .HasOne(vi => vi.Influencer)
            .WithMany(i => i.Voters)
            .HasForeignKey(vi => vi.InfluencerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<VoterInfluencer>()
            .HasIndex(vi => new
            {
                vi.VoterId,
                vi.InfluencerId
            })
            .IsUnique();

        // --------------------------------------------------
        // Influencer geography relationships
        // --------------------------------------------------

        modelBuilder.Entity<Influencer>()
            .HasOne(i => i.Constituency)
            .WithMany()
            .HasForeignKey(i => i.ConstituencyId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Influencer>()
            .HasOne(i => i.Island)
            .WithMany()
            .HasForeignKey(i => i.IslandId)
            .OnDelete(DeleteBehavior.NoAction);

        // --------------------------------------------------
        // Influencer categories (managed list, seeded with the client's three)
        // --------------------------------------------------

        modelBuilder.Entity<InfluencerCategory>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(60);
            entity.HasIndex(c => c.Name).IsUnique();

            var seeded = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);

            entity.HasData(
                new InfluencerCategory { Id = new Guid("6f1c2a10-0b5e-4d39-9a51-1c0e7a3d5001"), Name = "MP", SortOrder = 1, CreatedAt = seeded },
                new InfluencerCategory { Id = new Guid("6f1c2a10-0b5e-4d39-9a51-1c0e7a3d5002"), Name = "Island Council", SortOrder = 2, CreatedAt = seeded },
                new InfluencerCategory { Id = new Guid("6f1c2a10-0b5e-4d39-9a51-1c0e7a3d5003"), Name = "GM Member", SortOrder = 3, CreatedAt = seeded });
        });

        modelBuilder.Entity<Influencer>()
            .HasOne(i => i.Category)
            .WithMany()
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        // --------------------------------------------------
        // Political parties (managed list; seeded with the parties registered with
        // the Elections Commission on 2026-10-05). Voter.PoliticalPartyId null = "Not known".
        // --------------------------------------------------

        modelBuilder.Entity<PoliticalParty>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(100);
            entity.Property(p => p.ShortName).HasMaxLength(10);
            entity.HasIndex(p => p.Name).IsUnique();

            var seeded = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);

            PoliticalParty Party(int n, string name, string shortName, bool isDefault = false) => new()
            {
                Id = new Guid($"8a3e5c20-4f1d-4b7a-9c2e-5d0f6b1a70{n:00}"),
                Name = name,
                ShortName = shortName,
                SortOrder = n,
                IsDefaultFilter = isDefault,
                CreatedAt = seeded
            };

            entity.HasData(
                Party(1, "Maldivian Democratic Party", "MDP", isDefault: true),
                Party(2, "People's National Congress", "PNC"),
                Party(3, "Jumhooree Party", "JP"),
                Party(4, "Maldives Development Alliance", "MDA"),
                Party(5, "Adhaalath Party", "AP"),
                Party(6, "Maldives National Party", "MNP"),
                Party(7, "People's National Front", "PNF"));
        });

        modelBuilder.Entity<Voter>()
            .HasOne(v => v.PoliticalParty)
            .WithMany()
            .HasForeignKey(v => v.PoliticalPartyId)
            .OnDelete(DeleteBehavior.NoAction);

        // --------------------------------------------------
        // Voter photos: one per voter, removed with the voter
        // --------------------------------------------------

        modelBuilder.Entity<VoterPhoto>(entity =>
        {
            entity.HasKey(p => p.VoterId);
            entity.Property(p => p.ContentType).HasMaxLength(20);
            entity.HasOne(p => p.Voter)
                .WithOne()
                .HasForeignKey<VoterPhoto>(p => p.VoterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --------------------------------------------------
        // Voter indexes
        // --------------------------------------------------

        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.NationalId)
            .IsUnique();

        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.ConstituencyCode);

        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.RegisteredIsland);

        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.AtollCode);

        // --------------------------------------------------
        // Authorization codes are stored only as a keyed hash
        // --------------------------------------------------

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.AuthorizationCodeHash).HasMaxLength(64);

            entity.HasIndex(u => u.AuthorizationCodeHash)
                .IsUnique()
                .HasFilter("[AuthorizationCodeHash] IS NOT NULL");
        });

        // --------------------------------------------------
        // Rights granted to roles and to individual users
        // --------------------------------------------------

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(p => new { p.RoleId, p.Permission });
            entity.Property(p => p.Permission).HasMaxLength(100);
            entity.HasOne(p => p.Role)
                .WithMany()
                .HasForeignKey(p => p.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.HasKey(p => new { p.UserId, p.Permission });
            entity.Property(p => p.Permission).HasMaxLength(100);
            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --------------------------------------------------
        // System settings (key/value)
        // --------------------------------------------------

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(s => s.Key);
            entity.Property(s => s.Key).HasMaxLength(100);
            entity.Property(s => s.Value).HasMaxLength(500);
        });

        /*
         * Do not add the unique Constituency.Code index yet.
         *
         * Existing manually created constituency records still
         * have null codes. Add that index only after those records
         * have been reconciled with the 93 official constituencies.
         */
    }

    // --------------------------------------------------
    // DbSets
    // --------------------------------------------------

    public DbSet<User> Users =>
        Set<User>();

    public DbSet<Role> Roles =>
        Set<Role>();

    public DbSet<UserRole> UserRoles =>
        Set<UserRole>();

    public DbSet<UserScope> UserScopes =>
        Set<UserScope>();

    public DbSet<Constituency> Constituencies =>
        Set<Constituency>();

    public DbSet<Island> Islands =>
        Set<Island>();

    public DbSet<ConstituencyIsland> ConstituencyIslands =>
        Set<ConstituencyIsland>();

    public DbSet<OtpRequest> OtpRequests =>
        Set<OtpRequest>();

    public DbSet<TrustedDevice> TrustedDevices =>
        Set<TrustedDevice>();

    public DbSet<AuthorizationCodeHistory>
        AuthorizationCodeHistories =>
            Set<AuthorizationCodeHistory>();

    public DbSet<AuditLog> AuditLogs =>
        Set<AuditLog>();

    public DbSet<Voter> Voters =>
        Set<Voter>();

    public DbSet<Encounter> Encounters =>
        Set<Encounter>();

    public DbSet<Pledge> Pledges =>
        Set<Pledge>();

    public DbSet<Influencer> Influencers =>
        Set<Influencer>();

    public DbSet<VoterPhoto> VoterPhotos =>
        Set<VoterPhoto>();

    public DbSet<PoliticalParty> PoliticalParties =>
        Set<PoliticalParty>();

    public DbSet<InfluencerCategory> InfluencerCategories =>
        Set<InfluencerCategory>();

    public DbSet<VoterInfluencer> VoterInfluencers =>
        Set<VoterInfluencer>();

    public DbSet<SystemSetting> SystemSettings =>
        Set<SystemSetting>();

    public DbSet<RolePermission> RolePermissions =>
        Set<RolePermission>();

    public DbSet<UserPermission> UserPermissions =>
        Set<UserPermission>();

   

    

}