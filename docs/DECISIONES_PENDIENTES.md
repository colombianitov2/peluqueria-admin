# Decisiones pendientes

Este registro impide que la implementación convierta supuestos en reglas de negocio. Ninguna opción se considera aprobada hasta que el usuario la confirme.

## Decisiones obligatorias antes de implementar

### 1. Generación del cobro semanal

Se debe definir:

- qué intervalo representa una semana cobrable;
- en qué momento se genera la obligación;
- cómo se trata la semana de ingreso y la semana de retiro;
- cómo se comporta el cálculo cuando la fecha de ingreso o retiro cae a mitad de semana.

**Restricción vigente:** el sábado no está confirmado como día de generación o cobro. No se asumirá ningún día.

### 2. Visibilidad y separación de repositorios

Elegir entre:

- un repositorio público que contenga el código y los GitHub Releases;
- un repositorio privado para el código y otro repositorio público que contenga solamente los archivos de actualización y sus GitHub Releases.

La segunda opción protege el código, pero aumenta la complejidad de publicación y exige que la automatización publique artefactos entre repositorios. Cualquier credencial necesaria existiría solo en el entorno seguro de publicación; nunca dentro de la aplicación instalada.

### 3. Versiones de Windows compatibles

Definir las versiones y arquitecturas mínimas admitidas, por ejemplo:

- Windows 10 y Windows 11, o solamente Windows 11;
- procesadores x64 y, si se requiere, ARM64.

Esta decisión afecta el instalador, las pruebas, el soporte y la estrategia de empaquetado.

### 4. Tecnología definitiva

Confirmar o rechazar la recomendación de `docs/ARQUITECTURA_PROPUESTA.md`:

- .NET con WPF y SQLite;
- Electron con TypeScript y SQLite;
- Tauri con interfaz web y SQLite.

No se generará el código base antes de esta confirmación.

### 5. Moneda

Elegir entre:

- aplicación de moneda única en USD;
- arquitectura preparada para admitir otras monedas en el futuro.

La moneda principal inicial es USD en ambos casos. No se implementará conversión de moneda ni tasas de cambio sin requisitos explícitos.

## Decisiones adicionales que conviene cerrar

### 6. Nombre del módulo de personas que pagan por usar el local

El nombre **Trabajadores y alquiler de sillas** está prohibido. Debe elegirse un nombre definitivo que no confunda este grupo con los colaboradores.

### 7. Colaboradores correspondientes a cada mes

Se debe definir cómo se determina quiénes participan en el reparto de un mes cuando una persona entra o sale durante ese mes. No se deben inventar prorrateos ni reglas laborales.

### 8. Importes pagados y presupuestados en el punto de equilibrio

Se debe precisar, por cada tipo de servicio u obligación, cuándo se usa el valor pagado y cuándo el presupuestado para evitar dobles conteos.

### 9. Política de copias de seguridad

Definir la frecuencia, cantidad de copias a conservar y ubicación elegida por el usuario. La arquitectura ya exige copia previa a migraciones importantes y restauración manual segura.

### 10. Tecnología de actualización y firma

Confirmar el uso propuesto de Velopack con GitHub Releases y decidir, antes de una distribución real, si los instaladores y ejecutables se firmarán con un certificado de firma de código.
