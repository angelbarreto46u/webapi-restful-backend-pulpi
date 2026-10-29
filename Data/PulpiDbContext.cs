using Microsoft.EntityFrameworkCore;
using webapi_restful_backend_pulpi.Models;

namespace webapi_restful_backend_pulpi.Data;

public sealed class PulpiDbContext : DbContext
{
    public PulpiDbContext(DbContextOptions<PulpiDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Transaccion> Transacciones => Set<Transaccion>();
    public DbSet<SesionCaja> SesionesCaja => Set<SesionCaja>();
    public DbSet<AuditoriaAccion> AuditoriaAcciones => Set<AuditoriaAccion>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.HasIndex(r => r.Nombre).IsUnique();
            entity.HasData(
                new Rol { IdRol = 1, Nombre = "Administrador", Descripcion = "Acceso completo al sistema." },
                new Rol { IdRol = 2, Nombre = "Cajero", Descripcion = "Registra movimientos y administra su sesión de caja." });
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Activo).HasDefaultValue(true);
            entity.HasOne(u => u.Rol).WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.IdRol).OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasIndex(c => new { c.Nombre, c.Tipo }).IsUnique();
            entity.HasCheckConstraint("CK_Categorias_Tipo", "[Tipo] IN ('Ingreso', 'Gasto')");
        });

        modelBuilder.Entity<Transaccion>(entity =>
        {
            entity.HasCheckConstraint("CK_Transacciones_MontoPositivo", "[Monto] > 0 OR [Estado] = 'ANULADA'");
            entity.HasCheckConstraint("CK_Transacciones_Estado", "[Estado] IN ('REGISTRADA', 'ANULADA')");
            entity.Property(t => t.Estado).HasDefaultValue("REGISTRADA");
            entity.Property(t => t.FechaRegistro).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(t => t.FechaMovimiento);
            entity.HasIndex(t => t.IdUsuarioRegistro);
            entity.HasIndex(t => t.IdCategoria);
            entity.HasOne(t => t.Categoria).WithMany(c => c.Transacciones)
                .HasForeignKey(t => t.IdCategoria).OnDelete(DeleteBehavior.Restrict).IsRequired();
            entity.HasOne(t => t.UsuarioRegistro).WithMany()
                .HasForeignKey(t => t.IdUsuarioRegistro).OnDelete(DeleteBehavior.Restrict).IsRequired();
            entity.HasOne(t => t.SesionCaja).WithMany(s => s.Transacciones)
                .HasForeignKey(t => t.IdSesionCaja).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SesionCaja>(entity =>
        {
            entity.HasCheckConstraint("CK_SesionesCaja_Estado", "[Estado] IN ('ABIERTA', 'CERRADA')");
            entity.HasCheckConstraint("CK_SesionesCaja_SaldoInicial", "[SaldoInicial] >= 0");
            entity.HasIndex(s => s.IdUsuarioApertura).IsUnique().HasFilter("[Estado] = 'ABIERTA'");
            entity.HasOne(s => s.UsuarioApertura).WithMany()
                .HasForeignKey(s => s.IdUsuarioApertura).OnDelete(DeleteBehavior.Restrict).IsRequired();
            entity.HasOne(s => s.UsuarioCierre).WithMany()
                .HasForeignKey(s => s.IdUsuarioCierre).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditoriaAccion>(entity =>
        {
            entity.HasIndex(a => a.FechaHora);
            entity.HasIndex(a => new { a.Entidad, a.IdRegistro });
            entity.HasOne(a => a.Usuario).WithMany()
                .HasForeignKey(a => a.IdUsuario).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
