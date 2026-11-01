# Modelo de datos

```mermaid
erDiagram
    users ||--o{ cash_sessions : "abre"
    users ||--o{ sales : "registra (cajero)"
    categories ||--o{ products : "agrupa"
    customers |o--o{ sales : "recibe"
    cash_sessions ||--o{ sales : "contiene"
    sales ||--|{ sale_items : "detalle"
    sales ||--|{ payments : "se paga con"
    products ||--o{ sale_items : "vendido en"

    users {
        int Id PK
        varchar Email UK
        varchar FullName
        varchar PasswordHash "BCrypt"
        varchar Role "Admin | Cajero"
        bool IsActive
        timestamptz CreatedAt
    }
    categories {
        int Id PK
        varchar Name UK
        varchar Description
    }
    products {
        int Id PK
        varchar Sku UK
        varchar Name
        int CategoryId FK
        numeric Price "con IGV, > 0"
        int Stock "CHECK >= 0"
        int MinStock
        bool IsActive "baja lógica"
    }
    customers {
        int Id PK
        varchar DocType "Dni | Ruc"
        varchar DocNumber UK
        varchar Name
        varchar Email
        varchar Phone
        varchar Address
    }
    cash_sessions {
        int Id PK
        int CashierId FK
        varchar Status "Abierta | Cerrada"
        timestamptz OpenedAt
        timestamptz ClosedAt
        numeric OpeningAmount
        numeric ExpectedCash
        numeric CountedCash
        numeric Difference
        varchar Notes
    }
    document_series {
        int Id PK
        varchar DocumentType "Boleta | Factura"
        varchar Code UK "B001, F001"
        int LastNumber
        bool IsActive
    }
    sales {
        int Id PK
        varchar DocumentType
        varchar Series "UK con Number"
        int Number
        int CustomerId FK "nullable"
        int CashierId FK
        int CashSessionId FK
        numeric GrossAmount
        numeric Discount
        numeric Subtotal "base imponible"
        numeric Igv
        numeric Total
        numeric AmountPaid
        numeric Change
        varchar Status "Emitida | Anulada"
        timestamptz CreatedAt
        timestamptz VoidedAt
        int VoidedById FK
        varchar VoidReason
    }
    sale_items {
        int Id PK
        int SaleId FK
        int ProductId FK
        varchar ProductName "snapshot"
        int Quantity
        numeric UnitPrice "snapshot"
        numeric LineTotal
    }
    payments {
        int Id PK
        int SaleId FK
        varchar Method "Efectivo | Tarjeta | Yape | Plin"
        numeric Amount
        varchar Reference "Nº operación"
    }
```

## Tablas

| Tabla | Propósito | Notas de diseño |
|---|---|---|
| `users` | Cuentas del sistema | Correo único en minúsculas; rol guardado como texto legible. |
| `categories` | Agrupación del catálogo | No se puede borrar si tiene productos (FK `Restrict`). |
| `products` | Catálogo e inventario | `Price` incluye IGV. `CHECK (Stock >= 0)` como última barrera. Baja lógica para no romper comprobantes históricos. |
| `customers` | Adquirentes | El número de documento es único e inmutable (se validó con módulo 11 / 8 dígitos). |
| `cash_sessions` | Turnos de caja | Índice `(CashierId, Status)` para encontrar rápido la caja abierta. Los campos del arqueo se llenan al cerrar. |
| `document_series` | Series y último correlativo | La fila se bloquea con `UPDATE` durante la venta: los correlativos son consecutivos y sin duplicados. |
| `sales` | Cabecera del comprobante | Índice único `(Series, Number)`. Los importes se guardan (no se recalculan) porque son un documento tributario emitido. Las ventas anuladas no se borran. |
| `sale_items` | Detalle | Guarda nombre y precio **al momento de la venta** (snapshot). |
| `payments` | Medios de pago | Una venta puede tener varios (pago mixto). El vuelto se guarda en `sales.Change`. |

## Tipos y convenciones

- Montos: `numeric(12,2)` en PostgreSQL (nunca `float`). En SQLite (solo pruebas/local) se mapean a `REAL` porque SQLite no agrega decimales en SQL.
- Fechas: `timestamp with time zone` en UTC. La conversión a hora de Lima ocurre en reportes y en la UI.
- Enums: almacenados como texto (`"Boleta"`, `"Efectivo"`) para que la base sea legible desde cualquier herramienta de BI.
- El esquema completo, generado desde las migraciones, está en [esquema-postgres.sql](esquema-postgres.sql).
