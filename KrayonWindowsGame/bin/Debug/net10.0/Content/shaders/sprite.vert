#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
layout(location = 3) in vec3 aTangent;
layout(location = 4) in vec3 aBiTangent;
layout(location = 5) in vec4 aInstanceMatrix0;
layout(location = 6) in vec4 aInstanceMatrix1;
layout(location = 7) in vec4 aInstanceMatrix2;
layout(location = 8) in vec4 aInstanceMatrix3;

out vec2 TexCoord;

uniform mat4 view;
uniform mat4 projection;

void main()
{
    mat4 aInstanceMatrix = mat4(aInstanceMatrix0, aInstanceMatrix1, aInstanceMatrix2, aInstanceMatrix3);
    
    vec4 worldPos = aInstanceMatrix * vec4(aPosition, 1.0);
    
    TexCoord = aTexCoord;
    
    gl_Position = projection * view * worldPos;
}