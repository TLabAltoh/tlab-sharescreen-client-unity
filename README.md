# TLabShareScreen_Client_Unity_Android
Client programme for decoding packets from TLabShareScreen_Server

## Demonstration

### localhost (127.0.0.1)

[youtube video is here](https://youtu.be/PK0eoB0jQ_M)

<video src="https://user-images.githubusercontent.com/121733943/210447171-dd79dcfd-c64e-460e-81b2-7078929e0ea3.mp4"></video>

### Android Device

[youtube video is here](https://youtu.be/g4nKSnYe6RA)

![DSC_0002](https://user-images.githubusercontent.com/121733943/211289979-46bfc2f3-c247-4015-b21d-ba5839f11a41.JPG)

## Operating environment
| Property |                          |
|--------- | ------------------------ |
| OS       | Android 10 ~ 12          |
| GPU      | Qualcomm Adreno 505, 619 | 

## Requires
| Property     | Value         |
| ------------ | ------------  |
| Color Space  | Gamma         |
| Graphics API | OpenGL ES 3.1 |

## Install

```
git clone https://github.com/TLabAltoh/TLabShareScreen_Client_Unity_Android.git

cd TLabShareScreen_Client_Unity_Android

git submodule update --init
```

## Note
- Very slow performance due to software encoder. Also, it does not work properly with Oculus quest.

## TODO
- Switch from Unity's compute shaders to OpenCL

## link  
[Screencast server is here](https://github.com/TLabAltoh/TLabShareScreen_Server)
