# Fórmulas financieras

Todas las operaciones monetarias usan unidades menores enteras. Los redondeos necesarios se hacen al centavo y no alteran datos históricos.

## Uso del local

```text
deuda acumulada = máximo(suma de cuotas generadas - suma de pagos válidos, 0)
saldo a favor = máximo(suma de pagos válidos - suma de cuotas generadas, 0)
```

Al ingresar la deuda es cero. La primera cuota se causa tras siete días completos y vence el primer sábado igual o posterior al final del periodo. Los periodos siguientes avanzan siete días desde la fecha de ingreso; registrar un pago nunca reinicia ese ciclo. No se cobra un periodo incompleto y cada cuota ya causada conserva su tarifa histórica. Las cuotas futuras usan la tarifa vigente al inicio de cada periodo.

Se permite cualquier pago positivo, incluso con deuda cero. El pago se aplica primero a las cuotas causadas más antiguas. El excedente queda como saldo a favor y cubre automáticamente las próximas cuotas, por trabajador y sin mezclar cuentas. La proyección avanza hasta la primera cuota que el crédito no cubre completamente e informa en un solo dato la fecha y el importe del próximo pago requerido, además de la última fecha cubierta. Retirar al trabajador detiene las cuotas futuras, pero conserva su crédito; esta fase no implementa devoluciones.

## Precio sugerido por silla

```text
monto por cubrir = máximo(0, meta mensual oficial
                            + gastos extraoficiales vigentes
                            - ventas y otros ingresos esperados)
precio mensual por silla = monto por cubrir ÷ sillas ocupadas vigentes
precio semanal sugerido = precio mensual × 12 ÷ 52
```

Los pagos actuales por uso del local no se restan porque crearían una fórmula circular. Sin sillas ocupadas no se divide entre cero. Los gastos extraoficiales no forman parte del Balance anual oficial.

## Inventario

```text
existencia = entradas iniciales + compras - ventas - consumos + ajustes de conteo
margen informativo = venta bruta - costo promedio estimado de lo vendido
```

La compra es la única salida de caja por adquirir inventario. Venta y consumo reducen existencias, pero el costo estimado no se registra de nuevo como salida de caja.

## Obligaciones y mantenimiento

En el resumen y balance se usan únicamente pagos reales de obligaciones. El catálogo conserva por separado el valor esperado para control y para el precio sugerido por silla. En mantenimiento, el resumen usa únicamente el costo real de registros realizados; el costo estimado permanece como dato de programación.

## Resumen mensual

```text
ingresos reales = cuotas de Uso del local devengadas y cubiertas
                + ventas registradas
                + otros ingresos registrados
gastos reales = compras reales de inventario
              + gastos
              + imprevistos
              + pagos reales de obligaciones
              + costos reales de mantenimientos realizados
ganancia neta antes de colaboradores = ingresos reales - gastos reales
fondo colaboradores = máximo(ganancia neta antes de colaboradores, 0) × porcentaje global
asignación individual = fondo colaboradores × participación interna individual
ganancia retenida por el local = ganancia neta antes de colaboradores - fondo colaboradores
```

El porcentaje no cambia el punto de equilibrio cero. Las compras, obligaciones y mantenimientos se incorporan por una sola ruta para evitar doble conteo.

## Cierre y distribución

El fondo positivo se divide según participaciones internas cuya suma máxima es 100 %. Los residuos de centavos se asignan de forma determinista por `Guid`. Si la suma es inferior a 100 %, la porción no asignada queda retenida por el local.

Un resultado base cero o negativo produce fondo cero y no crea deuda. Un cierre confirmado conserva mes, porcentaje, fondo, participantes e importes históricos.

Mientras el cierre permanezca confirmado, sus totales y asignaciones guardados prevalecen sobre cambios posteriores de porcentajes o registros editables al consultar el resumen mensual, el balance anual y Excel. Al reabrir, el mes vuelve a usar la fórmula dinámica.

## Balance anual

El balance suma los 12 resultados mensuales, distribuciones pagadas de snapshots confirmados y obligaciones pendientes como dato de control separado. Desglosa pagos reales por categoría y no incluye planes de reposición. El ajuste histórico reconcilia el desglose dinámico con snapshots anteriores. El indicador es `Positivo` cuando el resultado retenido acumulado es mayor o igual a cero y `Negativo` cuando es inferior a cero.

Las operaciones originales de ingresos y gastos se conservan para los cálculos internos. Flujo de caja no es un módulo visible, pero se exporta como hoja de trazabilidad en Excel.

## Distribución individual vigente desde Fase 4.7

Cada importe individual se calcula sobre el fondo global:

```text
fondo global = máximo(resultado base, 0) × porcentaje global
asignación individual = fondo global × participación interna individual
```

La suma de participaciones internas puede ser menor, pero nunca mayor, que 100 %. El faltante no se reparte automáticamente. Con USD 1.000, porcentaje global 20 % y participaciones 60 %, 20 %, 10 % y 10 %, el fondo es USD 200 y los importes son exactamente USD 120, USD 40, USD 20 y USD 20.

## Aportes de capital

Los aportes de colaboradores se registran en `CollaboratorContributions` y se excluyen por completo de `MonthlySummaryInput`, ingresos operativos, meta, faltante, resultado base, fondo de colaboradores y resultado retenido. Son capital/inversión histórica y no generan un porcentaje adicional.
