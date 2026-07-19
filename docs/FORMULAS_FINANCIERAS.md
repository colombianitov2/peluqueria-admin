# Fórmulas financieras

Todas las operaciones monetarias usan unidades menores enteras. Los redondeos necesarios se hacen al centavo y no alteran datos históricos.

## Uso del local

```text
deuda actual = suma de cuotas generadas - suma de pagos válidos
```

La primera cuota empieza al ingreso y cada siguiente exactamente siete días después. Una cuota usa la tarifa vigente al inicio de su periodo. Se rechaza cualquier pago mayor que la deuda.

## Inventario

```text
existencia = entradas iniciales + compras - ventas - consumos + ajustes de conteo
sugerencia mensual = máximo(0, necesidad del mes - existencia disponible)
margen informativo = venta bruta - costo promedio estimado de lo vendido
```

La compra es la única salida de caja por adquirir inventario. Venta y consumo reducen existencias, pero el costo estimado no se registra de nuevo como salida de caja.

## Obligaciones y mantenimiento

Para la meta mensual de una obligación se usa una sola cifra:

```text
si está pagada completamente: importe real acumulado
si está pendiente o parcial: importe esperado
```

Los pagos parciales reducen el saldo, pero no duplican el gasto esperado. El flujo de caja incluye únicamente pagos reales.

Para mantenimiento se usa costo real si fue ejecutado; de lo contrario, el costo estimado cuando corresponde al mes. Nunca se suman ambos.

## Resumen mensual

```text
ingresos disponibles = pagos por Uso del local + ventas brutas + otros ingresos

reserva opcional = máximo(presupuesto mensual configurado,
                           insumos opcionales reales)

meta mensual = obligaciones aplicables
             + compras y gastos aplicables
             + imprevistos
             + mantenimiento aplicable
             + reserva opcional
             + planes mensuales pendientes aplicables

faltante = máximo(0, meta mensual - ingresos disponibles)
resultado base = ingresos disponibles - meta mensual
fondo colaboradores = máximo(0, resultado base × porcentaje)
resultado retenido = resultado base - fondo colaboradores
```

El porcentaje no cambia el punto de equilibrio cero. Las compras, obligaciones y mantenimientos se incorporan por una sola ruta para evitar doble conteo.

## Cierre y distribución

El fondo positivo se divide en partes iguales entre los participantes confirmados. Primero se asignan centavos completos; los centavos residuales se entregan uno por uno siguiendo el orden estable de `Guid`. Así, la suma de pagos individuales coincide exactamente con el fondo.

Un resultado base cero o negativo produce fondo cero y no crea deuda. Un cierre confirmado conserva mes, porcentaje, fondo, participantes e importes históricos.

## Balance anual y caja

El balance suma los 12 resultados mensuales, distribuciones pagadas y obligaciones pendientes sin repetir obligaciones anuales ya incluidas en un mes. El flujo de caja lista solo entradas y salidas efectivas dentro del rango seleccionado.

