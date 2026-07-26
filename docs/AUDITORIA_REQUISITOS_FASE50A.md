# Auditoría de aportes de colaboradores — Fase 5.0A

Fecha: 25 de julio de 2026.

## Causa raíz y corrección

- El historial visible usa eventos inmutables de aporte, pero la selección anterior intentaba convertir la fila directamente en un aporte vigente. Por eso nunca encontraba el registro estable asociado y no habilitaba una edición o eliminación válida.
- Cada evento visible conserva y usa ahora su `ContributionId` para resolver el aporte vigente exacto.
- Seleccionar o hacer doble clic en un evento de un aporte activo carga su fecha, valor y descripción en el formulario existente.
- Editar crea un evento `Aporte editado`, refresca inmediatamente el historial y los totales, y limpia la selección.
- Eliminar exige la confirmación visible, aplica eliminación lógica, crea un evento `Aporte eliminado`, excluye el importe de los totales vigentes y conserva toda la trazabilidad.
- Las filas que no son aportes, los eventos eliminados y los identificadores inexistentes no habilitan editar ni eliminar.
- Se mantiene un solo historial cronológico. La expresión visual `Participación pendiente` fue eliminada y no se añadió ningún selector alternativo.

## Alcance protegido

- No se modificaron el modelo de dominio, el esquema SQLite, las migraciones, los cierres, las distribuciones, las fórmulas financieras ni el pago completo al colaborador.
- Los aportes continúan siendo capital no operativo y no alteran la utilidad distribuible ni el fondo de colaboradores.
- La migración aislada desde el esquema alpha.1 conserva sus datos y permite crear, editar y eliminar lógicamente aportes después de migrar.

## Validación

- Compilación Debug y Release: cero advertencias y cero errores.
- Pruebas por configuración: 304 de 304 aprobadas en Debug y 304 de 304 en Release; 608 ejecuciones aprobadas en total.
- Pruebas nuevas: selección exacta, carga del formulario, edición, eliminación lógica, actualización inmediata, trazabilidad, limpieza de selección, estados de botones, filas inválidas, contrato XAML, migración desde alpha.1 y ausencia de cambios en la fórmula financiera.
- `dotnet format --verify-no-changes`: aprobado.
- Modelo EF Core: sin cambios pendientes.
- Auditoría NuGet con dependencias transitivas: sin vulnerabilidades conocidas ni paquetes en desuso.
- Gitleaks 8.30.1 con `--redact`: sin hallazgos en historial ni worktree.
- Revisión visual Release con datos temporales aislados: dos aportes creados, segundo aporte seleccionado desde el historial, editado de USD 25,00 a USD 35,50 y eliminado lógicamente; formulario y botones quedaron limpios tras cada operación.
- Base temporal de la revisión: `integrity_check=ok`, cero infracciones de claves foráneas, diez migraciones, dos aportes históricos, un aporte activo por USD 10,00 y cuatro eventos trazables.

## Limitaciones

- No se realizó una prueba manual con lector de pantalla. Los estados relevantes sí se validaron mediante el árbol de accesibilidad de Windows.
- La revisión visual se realizó en la escala activa del sistema; el contrato de diseño adaptable y las pruebas automatizadas existentes cubren las escalas adicionales sin introducir cambios visuales fuera de Colaboradores.
