# Guía de la API (ejemplos con curl)

Base: `http://localhost:5080` (Docker o `dotnet run`). La especificación completa e interactiva está en `/swagger`.

Los enums viajan como texto: `Boleta`/`Factura`, `Dni`/`Ruc`, `Efectivo`/`Tarjeta`/`Yape`/`Plin`, `Admin`/`Cajero`.

## 1. Autenticación

```bash
curl -s -X POST http://localhost:5080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"cajero@demo.pe","password":"Demo123!"}'
```

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-11-02T04:00:00Z",
  "user": { "id": 2, "email": "cajero@demo.pe", "fullName": "Lucía Quispe (demo)", "role": "Cajero", "isActive": true }
}
```

Guarde el token para las siguientes llamadas:

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"cajero@demo.pe","password":"Demo123!"}' | jq -r .accessToken)
```

## 2. Abrir caja

```bash
curl -s -X POST http://localhost:5080/api/cash-sessions/open \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"openingAmount": 200}'
```

## 3. Buscar productos (paginación, filtro y orden)

```bash
curl -s "http://localhost:5080/api/products?search=arroz&page=1&pageSize=10&sortBy=price&sortDir=desc" \
  -H "Authorization: Bearer $TOKEN"
```

```json
{ "items": [ { "id": 5, "sku": "ABA-001", "name": "Arroz extra 5 kg", "price": 24.90, "stock": 50, "isLowStock": false, "...": "..." } ],
  "page": 1, "pageSize": 10, "totalItems": 1, "totalPages": 1 }
```

## 4. Registrar una venta (boleta con pago mixto)

```bash
curl -s -X POST http://localhost:5080/api/sales \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{
        "documentType": "Boleta",
        "customerId": null,
        "discount": 0,
        "items": [ { "productId": 5, "quantity": 2 }, { "productId": 1, "quantity": 3 } ],
        "payments": [
          { "method": "Yape", "amount": 40, "reference": "OP-778899" },
          { "method": "Efectivo", "amount": 30 }
        ]
      }'
```

Respuesta `201 Created` (resumen):

```json
{
  "documentNumber": "B001-00000028",
  "grossAmount": 60.30, "discount": 0, "subtotal": 51.10, "igv": 9.20, "total": 60.30,
  "amountPaid": 70.00, "change": 9.70, "status": "Emitida"
}
```

## 5. Factura (requiere cliente con RUC)

```bash
# Buscar el cliente por documento
curl -s http://localhost:5080/api/customers/by-document/20601234565 -H "Authorization: Bearer $TOKEN"

curl -s -X POST http://localhost:5080/api/sales \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"documentType":"Factura","customerId":1,"items":[{"productId":10,"quantity":1}],
       "payments":[{"method":"Tarjeta","amount":21.90,"reference":"VOUCHER-001"}]}'
```

## 6. Errores típicos (ProblemDetails)

Regla de negocio — `422`:

```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Regla de negocio incumplida",
  "status": 422,
  "detail": "La factura requiere un cliente con RUC.",
  "code": "factura_sin_ruc",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

Validación — `400`:

```json
{
  "title": "Datos de entrada inválidos",
  "status": 400,
  "errors": { "docNumber": ["RUC inválido: debe tener 11 dígitos, prefijo 10/15/17/20 y dígito verificador correcto."] }
}
```

Códigos de negocio posibles: `caja_no_abierta`, `stock_insuficiente`, `factura_sin_ruc`, `boleta_sin_cliente`, `pago_insuficiente`, `pago_excedido`, `descuento_excedido`, `venta_anulada`, `producto_invalido`, `cliente_invalido`.

## 7. Cerrar caja (arqueo)

```bash
curl -s http://localhost:5080/api/cash-sessions/current -H "Authorization: Bearer $TOKEN"   # obtiene id y efectivo esperado

curl -s -X POST http://localhost:5080/api/cash-sessions/12/close \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"countedCash": 225.50, "notes": "Billete de S/ 10 deteriorado"}'
```

```json
{ "status": "Cerrada", "openingAmount": 200, "salesCount": 1, "expectedCash": 220.30,
  "countedCash": 225.50, "difference": 5.20,
  "paymentsByMethod": [ { "method": "Efectivo", "amount": 20.30 }, { "method": "Yape", "amount": 40 }, "..." ] }
```

## 8. Operaciones de administrador

```bash
ADMIN=$(curl -s -X POST http://localhost:5080/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.pe","password":"Demo123!"}' | jq -r .accessToken)

# Anular una venta (repone stock)
curl -s -X POST http://localhost:5080/api/sales/28/void \
  -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d '{"reason":"Cliente desistió de la compra"}'

# Crear producto
curl -s -X POST http://localhost:5080/api/products \
  -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d '{"sku":"BEB-010","name":"Agua sin gas 2.5 L","categoryId":1,"price":4.50,"stock":60,"minStock":12}'

# Ajuste de stock (merma)
curl -s -X POST http://localhost:5080/api/products/2/stock-adjustments \
  -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d '{"delta":-3,"reason":"Botellas dañadas"}'

# Reportes (fechas en hora de Lima, rango máximo 366 días)
curl -s "http://localhost:5080/api/reports/sales-by-day?from=2026-10-01&to=2026-10-31" -H "Authorization: Bearer $ADMIN"
curl -s "http://localhost:5080/api/reports/sales-by-product?from=2026-10-01&to=2026-10-31&top=10" -H "Authorization: Bearer $ADMIN"
curl -s "http://localhost:5080/api/reports/sales-by-cashier?from=2026-10-01&to=2026-10-31" -H "Authorization: Bearer $ADMIN"

# Crear un cajero
curl -s -X POST http://localhost:5080/api/users \
  -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d '{"email":"nuevo@demo.pe","fullName":"Cajero Nuevo","password":"Segura123","role":"Cajero"}'
```

## 9. Salud

```bash
curl -s http://localhost:5080/health   # "Healthy" (200) o "Unhealthy" (503) si no hay base de datos
```
