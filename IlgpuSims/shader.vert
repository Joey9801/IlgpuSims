#version 430 core

layout(location = 0) in vec2 aPosition;
out vec2 texCoord;

void main(void)
{
    texCoord = aPosition / 2 + vec2(0.5);
    gl_Position = vec4(aPosition, 0.0, 1.0);
}