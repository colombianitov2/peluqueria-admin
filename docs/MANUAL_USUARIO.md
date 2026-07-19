# Manual de usuario

## Primer inicio

La aplicación crea sus carpetas locales y aplica migraciones automáticamente. Inicio muestra únicamente el mes actual, obligaciones pendientes, deudas por Uso del local y el faltante mensual.

Los formularios usan fechas `AAAA-MM-DD`, meses `AAAA-MM`, importes con dos decimales y cantidades de inventario con hasta tres. Los botones deshabilitados indican que falta una selección o dato válido.

## Operación habitual

### Uso del local

Registre nombre, ingreso y retiro opcional. Las cuotas se generan desde el ingreso cada siete días. Para registrar un pago, seleccione la persona e indique fecha y monto; no se aceptan sobrepagos.

### Colaboradores y nómina

Registre colaboradores por fechas. En Nómina, seleccione el mes, previsualice participantes y cierre. El cierre guarda una fotografía. Registre después los pagos de distribución. Reabrir exige confirmación y queda trazado.

### Inventario y ventas

Primero cree el producto. Registre existencia inicial o compra antes de vender/consumir. Una venta calcula total, costo estimado y margen; no permite existencia negativa. El conteo físico crea un ajuste sin gasto. El plan mensual sugiere solo lo faltante y no mueve dinero hasta registrar la compra.

### Otros ingresos, gastos e imprevistos

Registre fecha, concepto y monto. No repita aquí una compra creada desde Inventario ni otro movimiento generado por un módulo específico.

### Obligaciones y mantenimiento

Las obligaciones admiten recurrencia mensual/anual y pagos parciales. El estado se calcula. En mantenimiento, indique costo estimado para la planeación y costo/fecha real al ejecutar; el sistema evita sumar ambos.

### Reportes

- Resumen mensual: escriba el mes y pulse **Consultar**.
- Balance anual: escriba el año y consulte.
- Flujo de caja: indique fechas inicial/final y categoría opcional.

### Editar y eliminar

Seleccione una fila y use **Editar/Cargar** para llevar sus valores al formulario. Guarde el cambio. Para eliminar, marque la confirmación visible y pulse **Eliminar**. La eliminación es lógica; cierres confirmados no cambian silenciosamente.

## Ajustes

Permite configurar tarifa semanal, porcentaje de colaboradores, reserva opcional, sillas y moneda. Cambiar moneda no convierte importes previos; cambiar tarifa solo afecta cuotas futuras.

En **Datos** puede crear/restaurar copias, abrir las carpetas y exportar CSV. Reinicie después de restaurar.

En **Actualizaciones** puede buscar, descargar e instalar una versión publicada. Sin Internet, el resto de la administración local continúa funcionando.

## Límites de esta alpha

- No es contabilidad oficial ni calcula impuestos legales.
- No registra servicios personales, clientes, proveedores, medios de pago ni comprobantes.
- El instalador no está firmado y SmartScreen puede advertirlo.
- La actualización real entre Releases no se ha verificado todavía.

