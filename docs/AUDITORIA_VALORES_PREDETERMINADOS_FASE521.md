# Auditoría de valores predeterminados — Fase 5.2.1

Fecha de corte: 29 de julio de 2026. Rama candidata: `codex/phase-5.2.1-no-defaults`.

## Alcance

Se revisaron modelos de dominio, DTO, casos de uso, servicios, calculadoras, ViewModels, XAML,
configuración de Entity Framework, migraciones, inicialización, exportaciones, pruebas y
documentación. La auditoría distingue un dato editable ausente de un cero contable, una propiedad
derivada, una etiqueta visual o una regla fija del producto.

## Hallazgos corregidos

| Zona | Supuesto eliminado | Comportamiento de Fase 5.2.1 |
|---|---|---|
| Ajustes | Tarifa diaria USD 12 y porcentaje global 20 % | Ambos son anulables y una instalación nueva los deja sin configurar |
| Colaboradores | Porcentajes individuales creados como 0 % | Cada porcentaje individual comienza nulo; cero exige un guardado explícito |
| Historial de tarifa | Creación automática de una primera vigencia | La primera vigencia nace únicamente al guardar una tarifa, incluido cero explícito |
| Exportación | Carpeta Escritorio | La ruta comienza vacía y la exportación exige elegir una carpeta |
| Formularios | Fecha actual, primera acción, tipo, frecuencia, categoría o método | Los campos comienzan vacíos y la validación identifica lo que falta |
| Ventas e inventario | Copia automática de precio, cantidad, costo y fecha | Solo se muestran datos anteriores como referencia; la captura nueva permanece vacía |
| Consultas | Hoy, este mes, todos o primer equipo | Los filtros comienzan sin selección y no muestran un periodo sustituto |
| Cálculos | Tarifa o porcentaje alternativo | Se muestra `Sin configurar` o `Sin calcular` y no se ejecuta el cálculo dependiente |
| Migraciones | Columnas obligatorias para tarifa y porcentaje | La migración correctiva las vuelve anulables sin borrar ni reinterpretar valores existentes |
| Pruebas | Valores 12/20 tratados como estado productivo | Los escenarios heredados los configuran expresamente mediante una utilidad exclusiva de pruebas |
| Documentación | Reglas antiguas de USD 12, 20 %, Escritorio y Hoy | La fuente canónica declara que Fase 5.2.1 las reemplaza |

## Valores revisados que no son configuración editable

- `USD` continúa como moneda única e invariable aprobada; no es una lista editable ni un valor
  financiero inventado.
- Los ceros de acumuladores, conteos, diferencias y sumas vacías son identidades matemáticas o
  estados derivados, no sustitutos de un campo del administrador.
- Un movimiento de inventario sin importe en efectivo aporta cero al flujo porque representa una
  corrección no monetaria; no se inventa un precio.
- Las columnas con `defaultValue: 0` de migraciones históricas corresponden a banderas, contadores,
  estados técnicos o importes obligatorios de entidades ya creadas. No se reescribieron migraciones
  publicadas.
- `DateTime.Today` permanece únicamente en indicadores de la fecha del sistema, evaluación de
  vigencia o cálculos derivados. Ya no inicializa controles de fecha editables.
- `SelectedIndex="0"` en el `TabControl` de Inventario y los índices de pestañas de perfiles son
  navegación estructural, no datos ni opciones configurables.
- Importes como 12 y porcentajes como 20 que aparecen en ejemplos o pruebas son entradas explícitas
  del escenario y no se ejecutan en una instalación real.

## Conservación y migración

La migración `20260729223813_Phase521NoEditableDefaults` cambia nulabilidad en Ajustes y en los
porcentajes individuales de Colaboradores, y añade el final de fecha exclusivo para representar
periodos sin tarifa. No contiene sentencias para borrar, poner a nulo o reclasificar una tarifa o
porcentaje existente. Por eso se conservan tanto un cero guardado como USD 12, 20 % o cualquier otro
valor real. Tampoco elimina vigencias, eventos financieros, actividad o cierres históricos.

En una base completamente nueva, el inicializador crea el contenedor de Ajustes sin tarifa ni
porcentaje y no crea filas en `DailyRates`, eventos financieros o actividad de configuración. El
primer guardado explícito crea la primera vigencia cuando corresponde.

## Criterio de aceptación

La auditoría se considera satisfecha cuando las pruebas de instalación limpia, cero explícito,
preservación por migración, culturas `es-CO`/`en-US`, compilaciones Debug/Release, formato, EF,
dependencias, secretos y revisión visual aislada terminan correctamente. Los resultados exactos se
registran en el PR borrador; no se toca la instalación ni la base real.
