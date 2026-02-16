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

    // Aplicar solo rotacion y escala del worldMatrix (ignorar su traslacion)
    // y aplicar la view SIN traslacion (solo mat3) para que el skybox
    // siempre este centrado en la camara
    mat4 viewNoTranslation = mat4(mat3(view));

    vec4 worldPos = worldMatrix * vec4(aPosition, 1.0);
    vec4 viewPos = viewNoTranslation * worldPos;

    TexCoord = aTexCoord;

    // Forzar el skybox al far plane (maxima profundidad)
    vec4 pos = projection * viewPos;
    gl_Position = pos.xyww;
}
