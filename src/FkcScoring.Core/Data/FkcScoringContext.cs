using FkcScoring.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FkcScoring.Core.Data;

public class FkcScoringContext : DbContext
{
    public FkcScoringContext(DbContextOptions<FkcScoringContext> options) : base(options) { }

    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Categorie> Categories => Set<Categorie>();
    public DbSet<Inscription> Inscriptions => Set<Inscription>();
    public DbSet<Equipe> Equipes => Set<Equipe>();
    public DbSet<EquipeMembre> EquipeMembres => Set<EquipeMembre>();
    public DbSet<Tableau> Tableaux => Set<Tableau>();
    public DbSet<Aire> Aires => Set<Aire>();
    public DbSet<Combat> Combats => Set<Combat>();
    public DbSet<EvenementCombat> EvenementsCombat => Set<EvenementCombat>();
    public DbSet<Kata> Katas => Set<Kata>();
    public DbSet<KataConfrontation> KataConfrontations => Set<KataConfrontation>();
    public DbSet<VoteJuge> VotesJuges => Set<VoteJuge>();
    public DbSet<Classement> Classements => Set<Classement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Le combat pointe deux fois vers Participant (Aka/Ao) : FK nullables, EF applique
        // ClientSetNull par convention, pas de chemin de cascade ambigu à résoudre ici.
        modelBuilder.Entity<Combat>()
            .HasOne(c => c.ProchainCombat)
            .WithMany()
            .HasForeignKey(c => c.ProchainCombatId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<KataConfrontation>()
            .HasOne(c => c.ProchainConfrontation)
            .WithMany()
            .HasForeignKey(c => c.ProchainConfrontationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supprimer une aire ne doit pas supprimer les tableaux qui y étaient affectés — juste les désassigner.
        modelBuilder.Entity<Tableau>()
            .HasOne(t => t.Aire)
            .WithMany()
            .HasForeignKey(t => t.AireId)
            .OnDelete(DeleteBehavior.SetNull);

        // Jeton de concurrence : détecte deux postes qui écrivent sur le même combat/confrontation
        // en parallèle (ex. arbitre + poste de contrôle sur le même tatami).
        modelBuilder.Entity<Combat>().Property(c => c.RowVersion).IsConcurrencyToken();
        modelBuilder.Entity<KataConfrontation>().Property(c => c.RowVersion).IsConcurrencyToken();

        modelBuilder.Entity<Kata>().HasData(SeedKatas());
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        IncrementerVersionsDeConcurrence();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        IncrementerVersionsDeConcurrence();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Incrémente RowVersion sur chaque Combat/KataConfrontation modifié, pour que le jeton de concurrence change à chaque écriture.</summary>
    private void IncrementerVersionsDeConcurrence()
    {
        foreach (var entry in ChangeTracker.Entries<Combat>())
            if (entry.State == EntityState.Modified) entry.Entity.RowVersion++;
        foreach (var entry in ChangeTracker.Entries<KataConfrontation>())
            if (entry.State == EntityState.Modified) entry.Entity.RowVersion++;
    }

    private static Kata[] SeedKatas()
    {
        string[] noms =
        {
            "Heian Shodan", "Heian Nidan", "Heian Sandan", "Heian Yondan", "Heian Godan",
            "Tekki Shodan", "Tekki Nidan", "Tekki Sandan",
            "Bassai Dai", "Bassai Sho", "Kanku Dai", "Kanku Sho",
            "Empi", "Jion", "Jitte", "Hangetsu", "Gankaku",
            "Nijushiho", "Chinte", "Sochin", "Meikyo", "Unsu", "Wankan", "Jiin"
        };
        var list = new Kata[noms.Length];
        for (int i = 0; i < noms.Length; i++)
            list[i] = new Kata { Id = i + 1, Nom = noms[i], Actif = true };
        return list;
    }
}
