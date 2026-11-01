# ADR-0002: `IAppDbContext` como puerto en lugar de repositorios genéricos

- **Estado:** aceptada
- **Fecha:** 2026-11-01

## Contexto

Clean Architecture pide que la capa de aplicación no dependa de la infraestructura. La forma clásica es crear `IRepository<T>` por entidad, pero EF Core ya implementa los patrones Repository (`DbSet<T>`) y Unit of Work (`DbContext`). Envolverlos suele producir interfaces que reexponen LINQ a medias, pierden `Include`, proyecciones y `ExecuteUpdate`, y añaden mucho código sin valor.

## Decisión

La capa Application define `IAppDbContext` con los `DbSet<T>` necesarios, `SaveChangesAsync` y `BeginTransactionAsync`. Application referencia solo el paquete base `Microsoft.EntityFrameworkCore` (sin proveedor); Infrastructure implementa la interfaz con `AppDbContext` y elige el proveedor (Npgsql o SQLite).

## Consecuencias

- ✅ Los servicios usan LINQ completo, proyecciones directas a DTO y operaciones masivas.
- ✅ El proveedor de base de datos se cambia por configuración; las pruebas usan SQLite sin tocar la aplicación.
- ⚠️ Application conoce la API de EF Core (no el proveedor). Es un acoplamiento consciente y aceptado: cambiar de ORM implicaría reescribir consultas de todas formas.
- ⚠️ Hay que evitar métodos específicos de proveedor relacional en Application (p. ej. `AsSplitQuery`), ya que solo el paquete base está referenciado.
