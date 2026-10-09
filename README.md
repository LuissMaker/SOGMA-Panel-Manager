# SOGMA Panel Manager

Código fuente y sistema de actualizaciones de **SOGMA Panel Manager**.

## Actualizaciones automáticas

La aplicación consulta las GitHub Releases de `LuissMaker/SOGMA-Panel-Manager`.

Para publicar una nueva versión:

1. Actualiza el código en `src/SOGMAPanelManager`.
2. Cambia `RELEASE_VERSION` al nuevo número, por ejemplo `1.0.9`.
3. Haz push a `main`.
4. GitHub Actions compilará la versión portable y creará una GitHub Release.

El asset generado se llama `SOGMA-Panel-Manager-vX.Y.Z.zip`.

## Carpetas

- `src/SOGMAPanelManager`: aplicación WPF .NET 8.
- `Assets`: recursos visuales.
- `PREMIERE`: prototipos/herramientas de integración con Premiere.
- `.github/workflows/release.yml`: compilación y publicación automática.
