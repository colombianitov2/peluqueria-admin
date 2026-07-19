# Plan de implementación de la Fase 3

Este documento registra el avance verificable para completar la primera versión alfa administrativa. Cada etapa conserva la migración `InitialSettings`, la ruta de datos aprobada y las funciones ya validadas de Inicio y Ajustes.

## Criterios transversales

- Rama: `feat/complete-administration` en un worktree aislado.
- Plataforma objetivo inicial: Windows 10/11 x64; validación principal en Windows 11.
- Dinero en unidades menores enteras y porcentajes en puntos básicos.
- Fechas financieras mediante `DateOnly`; auditoría y marcas técnicas en UTC.
- Eliminación lógica y transacciones para operaciones relacionadas.
- Ninguna prueba usa la base real del usuario.
- No se crean datos ficticios permanentes, etiquetas, Releases ni certificados.

## Etapas

| Etapa | Alcance | Verificación | Estado |
|---|---|---|---|
| 1 | Decisiones, modelo técnico y plan | Documentos coherentes con FASE 3 | Completada |
| 2 | Uso del local e Inventario | Pruebas de cuotas, pagos, existencias y reposición | Completada |
| 3 | Obligaciones, mantenimiento, ingresos, gastos y colaboradores | Pruebas de recurrencia, caja y eliminación lógica | Completada |
| 4 | Resumen mensual, cierres, nómina, balance anual y flujo de caja | Pruebas de fórmulas, centavos e inmutabilidad | Completada |
| 5 | Persistencia SQLite y casos de uso | Migración desde `InitialSettings` y pruebas temporales | Completada |
| 6 | Interfaz de los 15 módulos | Compilación WPF, navegación y prueba controlada | Completada |
| 7 | Copias, restauración y CSV | Pruebas con bases y carpetas temporales | Completada |
| 8 | Velopack y actualización | Empaquetado alfa x64 local; workflow sin publicar Release | Completada; actualización entre Releases no verificable aún |
| 9 | Documentación y validación final | Formato, builds, pruebas, auditoría, CI y revisión Git | Completada |

## Estrategia de commits y publicación

1. Núcleo administrativo y pruebas de dominio.
2. Persistencia y módulos operativos.
3. Reportes y cierres financieros.
4. Copias, exportación, interfaz y actualizaciones.
5. Validación integral y documentación final.

La rama se publicó después de cada bloque, el PR se abrió como borrador tras el primer commit sustancial y se marcó listo únicamente después de completar las verificaciones locales. El PR permanece sin fusionar y no se creó ninguna etiqueta ni Release.
