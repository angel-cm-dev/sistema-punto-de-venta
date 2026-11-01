# ADR-0001: Descuento de stock con UPDATE condicional atómico

- **Estado:** aceptada
- **Fecha:** 2026-11-01

## Contexto

Varias cajas venden simultáneamente. El patrón ingenuo "leer stock → validar en memoria → guardar" tiene una condición de carrera: dos cajas leen `stock = 1`, ambas validan y ambas venden, dejando el stock en `-1`.

Alternativas evaluadas:

1. **Bloqueo pesimista** (`SELECT ... FOR UPDATE`): correcto, pero requiere SQL específico de PostgreSQL y complica las pruebas con SQLite.
2. **Concurrencia optimista** (columna de versión / `xmin`): correcto, pero obliga a reintentar la venta completa ante conflictos frecuentes en productos populares.
3. **UPDATE condicional**: `UPDATE products SET stock = stock - @q WHERE id = @id AND stock >= @q` y comprobar las filas afectadas.

## Decisión

Se usa la opción 3 mediante `ExecuteUpdateAsync` de EF Core, dentro de una transacción que también reserva el correlativo e inserta la venta. Si algún ítem afecta 0 filas, se lanza `DomainException("stock_insuficiente")` y la transacción se revierte, devolviendo el stock de los ítems ya descontados.

Además se mantiene `CHECK (Stock >= 0)` en la tabla como defensa en profundidad.

## Consecuencias

- ✅ Sin carreras: la base de datos evalúa y escribe en una sola operación atómica.
- ✅ Portable (PostgreSQL y SQLite) y fácil de probar: existe una prueba de integración que verifica el rollback.
- ⚠️ `ExecuteUpdate` no pasa por el change tracker; por eso los productos se leen con `AsNoTracking` y no se guardan vía entidad en ese flujo.
