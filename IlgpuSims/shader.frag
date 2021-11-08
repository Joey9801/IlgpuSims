#version 430 core

out vec4 outputColor;
in vec2 texCoord;
uniform sampler2D texture0;

// Stolen from matplotlib's LinearSegmentedColormap for the Viridis color scale
const vec4 viridisData[8] = vec4[8](
    vec4(0.267004, 0.004874, 0.329415, 1.),
    vec4(0.275191, 0.194905, 0.496005, 1.),
    vec4(0.212395, 0.359683, 0.55171 , 1.),
    vec4(0.153364, 0.497   , 0.557724, 1.),
    vec4(0.122312, 0.633153, 0.530398, 1.),
    vec4(0.288921, 0.758394, 0.428426, 1.),
    vec4(0.626579, 0.854645, 0.223353, 1.),
    vec4(0.993248, 0.906157, 0.143936, 1.)
);

vec4 viridis(float val) {
    val = clamp(val, 0.0, 1.0);
    int lowerIdx = int(val * 7.0);
    int upperIdx = lowerIdx + 1;
    float t = val * 7.0 - float(lowerIdx);
    
    return mix(viridisData[lowerIdx], viridisData[upperIdx], t);
}

void main()
{
    outputColor = viridis(texture(texture0, texCoord).r);
} 