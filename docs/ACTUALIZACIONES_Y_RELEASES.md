# Actualizaciones y Releases

## Integración

- Biblioteca y CLI Velopack: 1.2.0.
- Id de paquete: `Colombianito.PeluqueriaAdmin`.
- Plataforma: Windows x64 autocontenido.
- Fuente: GitHub Releases públicos de `colombianitov2/peluqueria-admin`.
- No se incluye token en la aplicación. Las consultas públicas quedan sujetas al límite anónimo de GitHub.
- La base permanece en `%LocalAppData%`, fuera de la carpeta que Velopack reemplaza.

Velopack se inicializa antes que WPF. Después de mostrar la ventana, la aplicación comprueba actualizaciones en segundo plano; una falla de red se ignora para el arranque. En Ajustes, **Buscar actualización** permite comprobar y descargar manualmente. **Instalar y reiniciar** solo se habilita cuando existe un paquete descargado.

Las compilaciones preliminares consultan también GitHub prereleases. Las versiones estables omiten prereleases.

## Publicación deliberada

El workflow `.github/workflows/release.yml` solo se activa al empujar una etiqueta `v*` que contenga una versión SemVer válida, por ejemplo `v0.2.0-alpha.1`. El job:

1. restaura paquetes;
2. verifica formato;
3. ejecuta todas las pruebas Release;
4. publica Windows x64 autocontenido;
5. instala `vpk` 1.2.0 localmente;
6. descarga el feed anterior si existe para producir deltas;
7. crea instalador, portable y paquetes;
8. publica el GitHub Release con el token efímero del workflow.

No debe crearse una etiqueta hasta que el PR correspondiente esté aprobado y se decida publicar.

## Firma

La alpha no tiene firma. Windows SmartScreen puede mostrar una advertencia. El CLI admite parámetros de firma futuros, pero el workflow no contiene certificados, contraseñas ni referencias a un secreto inexistente. Un certificado futuro debe almacenarse como secreto de GitHub y nunca en Git.

## Verificación y límites

La versión `0.2.0-alpha.1` se publica como preliminar para instalar y verificar la migración desde
`0.1.0-alpha.1`. La prueba posterior de actualización automática entre dos Releases se realizará con
una versión distinta que incorpore el logotipo K&amp;V; no debe considerarse aprobada antes de ejecutar
ese salto real. Windows 10 x64 sigue siendo compatibilidad objetivo, no comprobada físicamente.

Fuentes oficiales consultadas:

- [Inicio con WPF](https://docs.velopack.io/getting-started/wpf)
- [Fuentes de actualización](https://docs.velopack.io/integrating/update-sources)
- [GitHub Actions](https://docs.velopack.io/distributing/github-actions)
- [CLI de Windows](https://docs.velopack.io/reference/cli/content/vpk-windows)
