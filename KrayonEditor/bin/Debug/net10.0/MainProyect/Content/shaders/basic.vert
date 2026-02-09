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

out vec3 FragPos;
out vec3 Normal;
out vec2 TexCoord;
out mat3 TBN;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform int u_UseInstancing;

void main()
{
    mat4 worldMatrix;
    
    if(u_UseInstancing == 1) {
        worldMatrix = mat4(aInstanceMatrix0, aInstanceMatrix1, aInstanceMatrix2, aInstanceMatrix3);
    } else {
        worldMatrix = model;
    }
    
    vec4 worldPos = worldMatrix * vec4(aPosition, 1.0);
    FragPos = worldPos.xyz;
    
    mat3 normalMatrix = transpose(inverse(mat3(worldMatrix)));
    Normal = normalize(normalMatrix * aNormal);
    
    vec3 T = normalize(normalMatrix * aTangent);
    vec3 B = normalize(normalMatrix * aBiTangent);
    vec3 N = Normal;
    TBN = mat3(T, B, N);
    
    TexCoord = aTexCoord;
    
    gl_Position = projection * view * worldPos;
}