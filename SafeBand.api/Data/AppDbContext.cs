using Microsoft.EntityFrameworkCore;
using SafeBand.api.Models;

namespace SafeBand.api.Data;

/// <summary>
/// Puente entre el código y SQL Server: define qué clases son tablas y cómo se configuran.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Cada DbSet = una tabla
    public DbSet<Zona> Zonas => Set<Zona>();
    public DbSet<Nodo> Nodos => Set<Nodo>();
    public DbSet<Alumno> Alumnos => Set<Alumno>();
    public DbSet<Tutor> Tutores => Set<Tutor>();
    public DbSet<TutorAlumno> TutoresAlumnos => Set<TutorAlumno>();
    public DbSet<Pulsera> Pulseras => Set<Pulsera>();
    public DbSet<LecturaBle> LecturasBle => Set<LecturaBle>();

    // Reglas extra que EF no puede adivinar por convención
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Zona>(e =>
        {
            e.Property(z => z.Nombre).HasMaxLength(100);
            e.Property(z => z.Tipo).HasConversion<string>().HasMaxLength(20); // guarda "Salon" en vez de 0
            e.HasIndex(z => z.Nombre).IsUnique();
        });

        modelBuilder.Entity<Nodo>(e =>
        {
            e.Property(n => n.Codigo).HasMaxLength(50);
            e.Property(n => n.Descripcion).HasMaxLength(200);
            e.HasIndex(n => n.Codigo).IsUnique();
            // No se puede borrar una zona si todavía tiene nodos
            e.HasOne(n => n.Zona).WithMany(z => z.Nodos).HasForeignKey(n => n.ZonaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Alumno>(e =>
        {
            e.Property(a => a.Nombres).HasMaxLength(100);
            e.Property(a => a.ApellidoPaterno).HasMaxLength(100);
            e.Property(a => a.ApellidoMaterno).HasMaxLength(100);
            e.Property(a => a.Matricula).HasMaxLength(30);
            // Matrícula única solo entre los que sí la tienen
            e.HasIndex(a => a.Matricula).IsUnique().HasFilter("[Matricula] IS NOT NULL");
        });

        modelBuilder.Entity<Tutor>(e =>
        {
            e.Property(t => t.Nombres).HasMaxLength(100);
            e.Property(t => t.ApellidoPaterno).HasMaxLength(100);
            e.Property(t => t.ApellidoMaterno).HasMaxLength(100);
            e.Property(t => t.Email).HasMaxLength(200);
            e.Property(t => t.Telefono).HasMaxLength(20);
            e.HasIndex(t => t.Email).IsUnique();   // un correo = un tutor
        });

        modelBuilder.Entity<TutorAlumno>(e =>
        {
            e.ToTable("TutoresAlumnos");
            // Llave compuesta: el mismo vínculo tutor-alumno no puede repetirse
            e.HasKey(ta => new { ta.TutorId, ta.AlumnoId });
            e.Property(ta => ta.Parentesco).HasMaxLength(50);
            // Si se borra un tutor o un alumno, se borran sus vínculos (no el otro lado)
            e.HasOne(ta => ta.Tutor).WithMany(t => t.Alumnos).HasForeignKey(ta => ta.TutorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ta => ta.Alumno).WithMany(a => a.Tutores).HasForeignKey(ta => ta.AlumnoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Pulsera>(e =>
        {
            e.Property(p => p.IdentificadorBle).HasMaxLength(50);
            e.Property(p => p.UidNfc).HasMaxLength(32);
            e.HasIndex(p => p.IdentificadorBle).IsUnique();
            // Único solo entre las que sí tienen UID (varias pueden estar en null)
            e.HasIndex(p => p.UidNfc).IsUnique().HasFilter("[UidNfc] IS NOT NULL");
            // Si se borra un alumno, sus pulseras quedan sin asignar (AlumnoId = null) en vez de borrarse
            e.HasOne(p => p.Alumno).WithMany(a => a.Pulseras).HasForeignKey(p => p.AlumnoId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LecturaBle>(e =>
        {
            e.ToTable("LecturasBle");
            // Índices para las consultas más comunes: "últimas lecturas" y "lecturas de una pulsera"
            e.HasIndex(l => l.Timestamp);
            e.HasIndex(l => new { l.PulseraId, l.Timestamp });
            // Las lecturas son historial: no se borran en cascada
            e.HasOne(l => l.Nodo).WithMany().HasForeignKey(l => l.NodoId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.Pulsera).WithMany().HasForeignKey(l => l.PulseraId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
