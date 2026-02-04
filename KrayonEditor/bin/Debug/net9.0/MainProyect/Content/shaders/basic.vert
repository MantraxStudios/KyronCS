#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
layout(location = 3) in vec3 aTangent;
layout(location = 4) in vec3 aBitangent;
layout(location = 5) in ivec4 aBoneIds;
layout(location = 6) in vec4 aBoneWeights;

const int MAX_BONES = 100;
uniform mat4 u_BoneMatrices[MAX_BONES];

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec3 FragPos;
out vec3 Normal;
out vec2 TexCoord;
out mat3 TBN;

void main()
{
    mat4 boneTransform = mat4(0.0);
    
    for(int i = 0; i < 4; i++)
    {
        if(aBoneIds[i] >= 0)
        {
            boneTransform += u_BoneMatrices[aBoneIds[i]] * aBoneWeights[i];
        }
    }
    
    if(boneTransform == mat4(0.0))
    {
        boneTransform = mat4(1.0);
    }
    
    vec4 localPosition = boneTransform * vec4(aPosition, 1.0);
    vec4 worldPosition = model * localPosition;
    
    FragPos = worldPosition.xyz;
    gl_Position = projection * view * worldPosition;
    
    mat3 normalMatrix = transpose(inverse(mat3(model * boneTransform)));
    Normal = normalize(normalMatrix * aNormal);
    
    TexCoord = aTexCoord;
    
    vec3 T = normalize(normalMatrix * aTangent);
    vec3 B = normalize(normalMatrix * aBitangent);
    vec3 N = Normal;
    TBN = mat3(T, B, N);
}