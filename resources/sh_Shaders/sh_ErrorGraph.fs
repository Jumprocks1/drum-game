#version 130

#include "sh_Utils.h"

varying mediump vec2 v_TexCoord;
varying mediump vec4 v_TexRect;
varying vec4 v_Colour;

uniform float m_AspectRatio;

const int SAMPLES = 64;

uniform float m_Samples[SAMPLES];

uniform float m_Scale;
uniform float m_Offset;

const float thickness = 0.015;


float BadWindow = 145.0;
float GoodWindow = 85.0;
float PerfectWindow = 40.0;

vec3 perfect = vec3(0,0.75,1);
vec3 good = vec3(0.486,0.988,0);
vec3 bad = vec3(1, 0.549, 0);
vec3 miss = vec3(0.690, 0, 0);

float dist(vec2 p)
{
    int sampleI = int(p.x * SAMPLES);

    float sample0 = m_Samples[clamp(sampleI - 1, 0, SAMPLES - 1)] / m_Scale;
    float sample1 = m_Samples[clamp(sampleI    , 0, SAMPLES - 1)] / m_Scale;
    float sample2 = m_Samples[clamp(sampleI + 1, 0, SAMPLES - 1)] / m_Scale;
    float sample3 = m_Samples[clamp(sampleI + 2, 0, SAMPLES - 1)] / m_Scale;
    
    return min(min(min(
        distance(vec2(float(sampleI - 1) / SAMPLES, sample0), p),
        distance(vec2(float(sampleI    ) / SAMPLES, sample1), p)
    ),
        distance(vec2(float(sampleI + 1) / SAMPLES, sample2), p)
    ), 
        distance(vec2(float(sampleI + 2) / SAMPLES, sample3), p)
    );
}

void main()
{
    float x = v_Colour.x - m_Offset;
    float y = (v_Colour.y - 0.5) / m_AspectRatio;


    float distanceToPlot = dist(vec2(x,y));
    float intensity = smoothstep(1, 0, distanceToPlot / thickness);
    intensity = pow(intensity, 1. / 4.0);

    float ms = abs(v_Colour.y - 0.5) * m_Scale;

    vec3 col = vec3(1,1,1);

    if (ms < PerfectWindow) {
        float a = ms / PerfectWindow;
        col = perfect * (1 - a) + good * a;
    } else if (ms < GoodWindow)  {
        float a = (ms - PerfectWindow) / (GoodWindow - PerfectWindow);
        col = good * (1 - a) + bad * a;
    } else if (ms < BadWindow)  {
        float a = (ms - GoodWindow) / (BadWindow - GoodWindow);
        col = bad * (1 - a) + miss * a;
    } else {
        col = miss;
    }
    gl_FragColor = vec4(col * intensity,1.0);
}