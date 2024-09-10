# tlab-sharescreen-client-unity
This is an [tlab-sharescreen-server-win](https://github.com/TLabAltoh/tlab-sharescreen-server-win) client program. Streaming video over a custom protocol (UDP) and implementing a frame decoder with compute shaders.

> [!WARNING]  
> Currently this program works well on local host (Windows), but very low performance on Android device. Maybe it is worth to replace unity's compute shader with OpenCL, but undecided to work on it. Because I don't intend to make this repository practical, because this repository is currently experimental.

## Screenshot

### localhost (127.0.0.1)

<video src="https://user-images.githubusercontent.com/121733943/210447171-dd79dcfd-c64e-460e-81b2-7078929e0ea3.mp4"></video>

### Android Device

![DSC_0002](https://user-images.githubusercontent.com/121733943/211289979-46bfc2f3-c247-4015-b21d-ba5839f11a41.JPG)

## Operating environment
| Property |                          |
| -------- | ------------------------ |
| OS       | Android 10 ~ 12          |
| GPU      | Qualcomm Adreno 505, 619 |

## Requires
| Property     | Value         |
| ------------ | ------------- |
| Color Space  | Gamma         |
| Graphics API | OpenGL ES 3.1 |

## Install

```
git clone https://github.com/TLabAltoh/tlab-sharescreen-client-unity.git

cd tlab-sharescreen-client-unity

git submodule update --init
```
