namespace PuntoVenta.Domain.Enums;

public enum Role { Admin = 1, Cajero = 2 }

/// <summary>Tipo de comprobante de pago electrónico (SUNAT).</summary>
public enum DocumentType { Boleta = 1, Factura = 2 }

/// <summary>Documento de identidad del cliente.</summary>
public enum IdentityDocType { Dni = 1, Ruc = 2 }

public enum PaymentMethod { Efectivo = 1, Tarjeta = 2, Yape = 3, Plin = 4 }

public enum SaleStatus { Emitida = 1, Anulada = 2 }

public enum CashSessionStatus { Abierta = 1, Cerrada = 2 }
