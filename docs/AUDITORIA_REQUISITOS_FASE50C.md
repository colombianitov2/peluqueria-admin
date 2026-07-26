# Auditoría de requisitos — Fase 5.0C

Fecha de corte: 26 de julio de 2026.

## Causas confirmadas

| Área | Causa | Corrección |
|---|---|---|
| Inventario | El formulario usaba anchos demasiado pequeños y las tablas no garantizaban un área útil a escalas altas. | Anchos proporcionales, formulario sin barra interna, página desplazable y tablas con barras propias. |
| Obligaciones y préstamos | La página no tenía desplazamiento vertical general y los pagos de obligaciones solo se podían crear. | Barra general; selección, edición, guardado y eliminación persistente de pagos; recálculo inmediato. |
| Mantenimiento | Dos tablas con filas elásticas quedaban fuera de pantalla. | Página desplazable, altura útil fija y barras propias en ambas tablas. |
| Resumen mensual | La vista exponía nombres internos del mecanismo de cierre y repetía compromisos. | Categorías comerciales, una sola tabla de pagos, deudas nombradas por alquiler de silla y un solo resultado Faltó/Sobró. |
| Obligaciones anuales | El pago real podía volver a sumarse completo después del prorrateo. | Doce prorrateos del esperado y solo diferencia real en el mes del pago. |
| Balance anual | Heredaba controles de periodo y no mostraba una fila por mes. | Selector exclusivo de año, resumen por categorías y detalle enero-diciembre. |
| Gráficos | El pastel de ingresos no tenía leyenda externa equivalente al de gastos. | Ambos pasteles usan leyendas externas con categoría, USD y porcentaje. |

## Fórmulas verificables

```text
valor esperado de compra = cantidad esperada × precio unitario esperado
valor real de compra = cantidad comprada × costo unitario real

total ingresado = saldo anterior + alquileres cobrados + ventas
                  + otros ingresos + demás ingresos reales
total gastado = suma única de todas las categorías de salida
punto de equilibrio = total gastado
diferencia = total ingresado - punto de equilibrio
saldo siguiente = diferencia + aportes + financiación recibida

categoría anual = suma de la categoría final de enero a diciembre
prorrateo anual mensual = total anual ÷ 12, con residuo en diciembre
ajuste al pagar = valor real pagado - total anual esperado
```

Un mes cerrado usa el snapshot confirmado. Un mes abierto usa los registros vigentes. Un mes futuro sin movimientos vale cero. Las hojas `Resúmenes mensuales` y `Balance anual` de Excel usan estas mismas categorías.

## Regresión protegida

- `alpha.1`, su instalación y sus datos reales no se modifican.
- No se cambia la regla global configurable de colaboradores.
- Las eliminaciones siguen siendo lógicas y los historiales permanecen exportables.
- El logotipo, la etiqueta, el Release y la prueba del actualizador quedan para una fase posterior.
