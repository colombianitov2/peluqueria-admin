# Arquitectura propuesta

## Alcance y estado

Este documento conserva la comparación técnica inicial y registra la arquitectura implementada hasta la Fase 3.

## Arquitectura adoptada (18 de julio de 2026)

Se adoptó la siguiente base tecnológica para el proyecto:

- **Lenguaje y plataforma:** C# con .NET 10.
- **Interfaz de escritorio:** WPF para Windows.
- **Datos locales:** SQLite.
- **Instalación y actualizaciones previstas:** Velopack.
- **Publicación:** un único repositorio público.
- **Canal de actualización:** GitHub Releases públicos del mismo repositorio.
- **Repositorio publicado:** [https://github.com/colombianitov2/peluqueria-admin](https://github.com/colombianitov2/peluqueria-admin), desde el 18 de julio de 2026.

La solución incorpora EF Core SQLite, migraciones, MVVM, inyección de dependencias, copias, exportación única con ClosedXML, pruebas y Velopack 1.2.0. Las decisiones realmente abiertas permanecen en `docs/DECISIONES_PENDIENTES.md`.

## Comparación de alternativas

| Criterio | .NET + WPF + SQLite | Electron + TypeScript + SQLite | Tauri + interfaz web + SQLite |
|---|---|---|---|
| Ajuste a Windows | Nativo y directo; WPF está diseñado para Windows | Bueno, mediante Chromium y Node.js empaquetados | Bueno, mediante WebView2 y backend en Rust |
| Estabilidad | Alta para una aplicación administrativa de escritorio | Alta, con un ecosistema amplio, pero con más piezas web | Buena, aunque incorpora Rust, WebView y plugins |
| Mantenimiento | Un lenguaje principal y un ecosistema integrado | Frontend web, proceso principal, puente seguro y módulos nativos | Frontend web, Rust y contratos entre ambos lados |
| Tamaño del instalador | Medio; puede publicarse autocontenido o dependiente del runtime | Alto, porque incluye Chromium y Node.js | Bajo en comparación, al reutilizar WebView2 |
| Calidad de interfaz | Muy buena con XAML, estilos, plantillas y enlace de datos | Muy buena y flexible con tecnologías web | Muy buena y flexible con tecnologías web |
| SQLite, copias y migraciones | Integración madura en .NET; buena separación por capas | Viable, pero los módulos SQLite nativos agregan complejidad de empaquetado | Viable mediante plugin o backend Rust; exige cuidar permisos y contratos |
| Actualizaciones | Velopack puede generar instalador y consumir GitHub Releases | `autoUpdater`/herramientas de empaquetado lo permiten, con mayor peso de distribución | El actualizador oficial exige artefactos firmados y configuración de claves |
| Dificultad operativa para una persona no técnica | Baja una vez instalado; experiencia tradicional de Windows | Baja para el usuario, pero más compleja para mantener y publicar | Baja para el usuario, pero más compleja para desarrollar y custodiar claves |
| Herramientas detectadas en este equipo | .NET SDK 10.0.301 disponible | Node 24.16.0 y npm 11.13.0 disponibles | Rust y Cargo no detectados |

Fuentes técnicas consultadas:

- [Introducción oficial a WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [Actualizaciones desde GitHub Releases con Velopack](https://docs.velopack.io/integrating/update-sources)
- [Conservación de datos durante actualizaciones con Velopack](https://docs.velopack.io/integrating/overview)
- [Actualizador oficial de Electron](https://www.electronjs.org/docs/latest/api/auto-updater/)
- [Actualizador oficial de Tauri](https://v2.tauri.app/plugin/updater/)

## Recomendación

Se recomienda **.NET con C#, WPF y SQLite**.

Razón principal: es la opción más simple y estable para una aplicación exclusivamente Windows, local y administrativa, con menor complejidad de mantenimiento que una solución que combine frontend web con Node.js o Rust.

Justificación:

- **Estabilidad:** WPF forma parte del ecosistema .NET para escritorio Windows y es apropiado para formularios, tablas, validación y navegación administrativa.
- **Mantenimiento:** C# puede cubrir interfaz, reglas, persistencia, copias, migraciones y actualización sin dividir el proyecto entre varios entornos de ejecución.
- **Tamaño del instalador:** será mayor que Tauri y normalmente menor que Electron. El tamaño exacto dependerá de si la publicación es autocontenida; debe medirse en un prototipo antes de fijar la estrategia.
- **Calidad de interfaz:** WPF ofrece XAML, estilos, plantillas y enlace de datos suficientes para una interfaz cuidada y consistente.
- **Copias de seguridad y migraciones:** .NET y SQLite permiten controlar transacciones, copias previas y restauración sin depender de un servicio en Internet.
- **Actualización:** Velopack separa el instalador y el actualizador de los datos de usuario, admite GitHub Releases como fuente y permite descargar para aplicar al reiniciar.
- **Uso no técnico:** el resultado puede ofrecer instalación inicial mediante ejecutable, inicio normal desde Windows, comprobación automática y el botón manual **Buscar actualizaciones**.

La versión concreta de .NET y todos los paquetes se fijarán solamente después de aprobar la tecnología y las versiones de Windows compatibles.

## Separación propuesta por capas

```text
Presentación WPF (MVVM)
        |
Casos de uso y validación de aplicación
        |
Dominio y reglas de negocio
        |
Infraestructura: SQLite, copias, migraciones, exportación y actualizaciones
```

- **Presentación:** pantallas, navegación, validación visible y comandos. No contiene fórmulas financieras ni acceso directo a SQLite.
- **Aplicación:** define contratos y coordina Ajustes, operaciones administrativas, transacciones, datos y actualizaciones.
- **Dominio:** concentra deuda semanal, inventario, obligaciones, mantenimiento, punto de equilibrio, caja, cierres y distribución.
- **Infraestructura:** implementa SQLite, transacciones, migraciones, copias, restauración y un único libro `.xlsx`. El adaptador Velopack vive en la composición WPF y satisface un contrato de Application.

Esta separación protege las reglas aprobadas y permite probarlas sin depender de la interfaz.

## Datos locales y protección

Almacenamiento adoptado para la aplicación:

- El ejecutable y sus archivos de versión viven en la ubicación administrada por el instalador.
- La base de datos vive en `%LocalAppData%\PeluqueriaAdmin\Data\peluqueria-admin.db`, fuera de la carpeta del ejecutable.
- Las carpetas `%LocalAppData%\PeluqueriaAdmin\Backups`, `Exports` y `Logs` separan copias, exportaciones y registros.
- Las pruebas inyectan una raíz temporal y no usan datos reales.

Reglas técnicas:

- SQLite usa claves foráneas, transacciones y restricciones para proteger integridad.
- Los importes monetarios se almacenan con una representación exacta definida; no se usa punto flotante binario para dinero.
- La configuración general obligatoria usa una sola fila protegida, con fechas UTC de creación y modificación.
- El dinero se persiste como unidades menores enteras; los porcentajes se persisten como puntos básicos.
- La moneda de aplicación es la constante USD. Las columnas históricas de moneda y presupuesto opcional se conservan solo para migración, se normalizan a USD/cero y no participan en reglas vigentes.

La migración `Phase46UsdExportsDistributionInventory` es aditiva: incorpora `Settings.ExportDirectory`, `Collaborators.ProfitShareBasisPoints` y `Products.DefaultUnitCostMinorUnits`. No reescribe importes, no elimina columnas heredadas y no altera snapshots de cierres. Los eventos de silla se escriben tanto para el trabajador como para la silla dentro de la misma transacción lógica, lo que permite perfiles coherentes sin inferir asignaciones nuevas.
- La eliminación funcional es lógica y las consultas normales excluyen registros eliminados.
- Cada migración se identifica por versión y se ejecuta dentro de una transacción cuando SQLite lo permita.
- Antes de una migración importante se crea una copia verificada de la base de datos cerrada o mediante el mecanismo seguro de copia de SQLite.
- Si una migración falla, se revierte la transacción y no se abre la aplicación sobre un esquema parcialmente actualizado.
- La restauración manual valida la copia elegida y crea una copia del estado actual antes de reemplazarlo.
- Se crea como máximo una copia automática diaria cuando cambia la base y se retienen las 30 automáticas más recientes.

Antes de crear cualquier base de datos o archivo de usuario, el repositorio deberá incorporar reglas de exclusión para bases reales, archivos auxiliares de SQLite, copias, configuraciones personales, registros, secretos, tokens y certificados.

## Actualizaciones propuestas

### Flujo recomendado

1. Usar versiones semánticas y etiquetas como `v1.0.0`.
2. El workflow de GitHub compila, prueba y empaqueta una etiqueta aprobada.
3. Velopack genera el instalador inicial y los artefactos de actualización.
4. Los artefactos se publican en un GitHub Release público.
5. La aplicación consulta el Release al iniciar y también mediante **Buscar actualizaciones**.
6. Si existe una versión estable posterior, la descarga y solicita cerrar o reiniciar para aplicarla.
7. Los datos permanecen fuera de la carpeta reemplazada por el actualizador.
8. Al iniciar la nueva versión, se crea la copia previa necesaria y se ejecutan las migraciones transaccionales.

El ejecutable contiene solo la dirección pública de actualizaciones; no contiene un token personal de GitHub.

### Opción A: código y lanzamientos públicos

Ventajas:

- configuración más sencilla;
- un solo repositorio, historial y proceso de lanzamiento;
- descargas públicas sin credenciales en la aplicación.

Costos:

- el código fuente y su historial quedan visibles públicamente;
- exige una revisión rigurosa para impedir que datos o secretos entren en Git.

### Opción B: código privado y lanzamientos públicos separados

Ventajas:

- el código y su historial permanecen privados;
- la aplicación puede descargar actualizaciones públicas sin almacenar un token.

Costos:

- proceso de publicación más complejo;
- la automatización necesita permiso limitado para publicar artefactos en el repositorio público;
- se deben coordinar etiquetas, notas y archivos entre dos repositorios.

### Estado adoptado de publicación

Se adoptó la opción A: un único repositorio público para código y lanzamientos. El repositorio está publicado en [https://github.com/colombianitov2/peluqueria-admin](https://github.com/colombianitov2/peluqueria-admin). La opción B se conserva arriba únicamente como parte de la comparación histórica.

Los GitHub Releases públicos son la fuente configurada para Velopack. La aplicación consulta sin credenciales; las publicaciones usan únicamente el `GITHUB_TOKEN` efímero del workflow.

## Validaciones aún pendientes

- actualización entre dos versiones de prueba conservando la base de datos;
- instalación limpia y actualización sobre cada versión de Windows aprobada;
- comparación de resultados mensuales y anuales con ejemplos manuales aprobados.
