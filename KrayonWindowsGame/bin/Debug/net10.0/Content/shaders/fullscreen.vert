#version 330 core

layout(location = 0) in vec2 a_Position;
layout(location = 1) in vec2 a_TexCoord;

out vec2 TexCoord;

void main()
{
    TexCoord = a_TexCoord;
    gl_Position = vec4(a_Position, 0.0, 1.0);
}