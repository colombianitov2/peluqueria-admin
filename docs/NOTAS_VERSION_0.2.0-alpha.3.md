# Peluquería Admin 0.2.0-alpha.3

Estado: **Versión preliminar candidata**. Esta preparación no publica una etiqueta, un instalador ni
un GitHub Release. Si posteriormente se aprueba y publica, el instalador objetivo será
`PeluqueriaAdmin-Setup-v0.2.0-alpha.3.exe`; como los alfas anteriores, no está firmado digitalmente.

## Corrección prioritaria Fase 5.2.1

- Todos los campos editables comienzan vacíos en una instalación nueva.
- Vacío se conserva como `Sin configurar`; cero solo representa un valor guardado expresamente.
- Se eliminan la tarifa diaria inicial, el porcentaje global inicial y la ruta de exportación inicial.
- Fechas, cantidades, precios, frecuencias, categorías, acciones y filtros ya no se preseleccionan.
- Los cálculos y las operaciones dependientes quedan sin calcular o bloqueados cuando falta su configuración.
- La primera tarifa guardada crea la primera vigencia; una instalación nueva no inventa historial.
- Las migraciones conservan los valores y el historial existentes, incluso cuando coinciden con antiguos valores predeterminados.

## Conservación

La corrección es incremental sobre Fase 5.2. Mantiene cobros diarios históricos, aportes, cierres,
inventario, obligaciones, préstamos, mantenimiento, copias, restauración, Excel, actualización y el
logotipo K&V. No modifica datos reales durante la preparación.
