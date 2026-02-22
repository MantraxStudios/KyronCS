#version 330 core

in vec3 FragPos;

uniform vec3 u_LightPos;
uniform float u_FarPlane;

layout(location = 0) out vec4 FragColor;

void main()
{
    float distance = length(FragPos - u_LightPos);
    distance = distance / u_FarPlane;
    // Store normalized distance as color (RGBA32f cubemap)
    FragColor = vec4(vec3(distance), 1.0);
}
