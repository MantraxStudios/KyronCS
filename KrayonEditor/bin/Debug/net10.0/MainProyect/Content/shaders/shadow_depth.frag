#version 330 core

layout(location = 0) out vec4 FragColor;

void main()
{
    // Store normalized depth as color (RGBA32f texture)
    float depth = gl_FragCoord.z;
    FragColor = vec4(vec3(depth), 1.0);
}
