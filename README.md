# ARSanbox_KinectV1

## Descripción

Este proyecto es una aplicación de ejemplo para Kinect v1 que captura datos de profundidad, mapea la información del sensor y la envía a través de la red. Está diseñada para funcionar con Kinect for Windows SDK 1.8 y un dispositivo Kinect v1.

## Qué hace la aplicación

- Lee la señal de profundidad del Kinect v1.
- Detecta y sigue la posición de las manos.
- Mapea los datos de profundidad a coordenadas visuales.
- Envía información a través de UDP/compartición de memoria para su uso en otras aplicaciones.
- Incluye ventanas WPF para previsualizar resultados y realizar pruebas locales.

## Cómo funciona

1. La aplicación inicializa el sensor Kinect v1.
2. Captura marcos de profundidad y, opcionalmente, color.
3. Realiza el procesamiento necesario para extraer datos de mano y modelos 3D.
4. Construye una malla o mapa de profundidad usando los datos del sensor.
5. Envía los datos procesados a través de la red usando UDP o una memoria compartida, según la implementación disponible.

## Requisitos

- Windows compatible con Kinect for Windows SDK 1.8.
- Kinect v1 (dispositivo Kinect original para Xbox 360 con adaptador para PC, o Kinect for Windows v1).
- Kinect for Windows SDK 1.8 instalado.
- Visual Studio con soporte para proyectos WPF/C# si se desea compilar desde el código fuente.

## Cómo ejecutar

1. Instala Kinect for Windows SDK 1.8.
2. Conecta el Kinect v1 al PC y asegúrate de que se detecta correctamente.
3. Abre la solución `ProyectSandbox\ProyectoSandbox.sln` en Visual Studio.
4. Compila el proyecto.
5. Ejecuta la aplicación desde Visual Studio o lanzando el ejecutable generado.

## Estructura del proyecto

- `ProyectSandbox/` - carpeta principal con el proyecto WPF.
- `MainWindow.xaml` / `MainWindow.xaml.cs` - ventana principal de la aplicación.
- `KinectDepthReader.cs` - lectura de datos de profundidad del Kinect.
- `OpenCvHandTracker.cs` - seguimiento de manos usando OpenCV.
- `DepthColorMapper.cs` - mapeo entre profundidad y color.
- `UdpHandSender.cs` - envío de datos de manos por UDP.
- `UdpDepthSender.cs` - envío de datos de profundidad por UDP.
- `SharedMemoryDepthSender.cs` - envío de datos por memoria compartida.
- `MeshBuilder.cs` / `MeshPreviewWindow.cs` - construcción y previsualización de mallas.
- `ObjExporter.cs` - exportación de objetos 3D.

## Nota

Este proyecto requiere hardware Kinect v1 y el SDK 1.8 porque usa APIs y dependencias específicas de esa versión del SDK.
