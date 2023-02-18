#version 300 es
precision highp float;


uniform vec2[100] uPoints;

uniform float iTime;
void mainImage(out vec4 fragColor);
in vec2 uv;
out vec4 oFragColor;
void main(void) {
    vec4 fragColor;
    mainImage(fragColor);
    oFragColor = fragColor;
}

const float tau = 3.1415926535*2.0;
const float pi = 3.1415926535;
const float pi12 = 3.1415926535 / 2.;

const float root3 = 1.7320508;

const vec3 grey = vec3(0.161, 0.165, 0.176); // #292a2d
const vec3 blue = vec3(0.141, 0.447, 0.784); // #2472c8

const vec3 colors[4] = vec3[4](
    grey,
    grey * 0.85 + blue * 0.15,
    grey * 1.1,
    grey * 0.95
);
const float colorLen = float(colors.length());


float random (vec2 st) {
    return fract(sin(dot(st.xy,vec2(12.9898,78.233)))*43758.5453123);
}

vec2 getHex(vec2 p)
{
    float x = p.x / root3;
    float y = p.y;
    
    float t1 = y;
    float t2 = floor(x + y);
    float r = floor((floor(t1 - x) + t2) / 3.0);
    float q = floor((floor(2.0 * x + 1.0) + t2) / 3.0) - r;
    return vec2(q,r);
}

vec3 hexBgColor() {
    vec3 color = vec3(0.);

    vec2 coord = getHex(uv*10.0 + iTime * vec2(0.5,2.0) + sin(iTime) * 0.3);
   
    float rand = random(coord) * colorLen;
    
    int colorIndex = int(rand);
    
    color += colors[0];
    
    color += (colors[colorIndex] - colors[0])*0.6;
    
    coord = getHex(uv.yx*4.0 + iTime * vec2(-0.2,0.6) + sin(iTime*0.2) * 2.0);
    colorIndex = int(mod(coord.x - coord.y, colorLen));
    
    rand = random(coord);
    
    colorIndex = int(rand * colorLen);
    color += (colors[colorIndex] - colors[0])*0.6;

    return color;
}

vec3 line() {
    const float lineRadius = 0.03;

    vec2 tangent = vec2(0.);    

    float dist = 100.;

    for(int i=0;i<uPoints.length();i+=2)
    {
        vec2 a = uPoints[i];
        vec2 b = uPoints[i+1];
        if (a == b) break;
        vec2 dir = b - a;
        float pos = clamp(dot(uv - a, dir) / dot(dir,dir),0.,1.0);
        vec2 linePos = a + pos*dir;
        float d = distance(linePos, uv) / lineRadius;
        if (d < dist) {
            dist = d;
            tangent = normalize(uv - linePos);
        }
    }

    return vec3(tangent, 1. - dist);
}

void mainImage(out vec4 fragColor)
{
    const float borderCenter = 0.95;

    const float baseMaxLighting = 1.7;
    const float targetWidth = .012; // width of maxed out lighting in the border
    const float bleedAmount = 1.2;

    vec3 color = vec3(0.);

    float r = length(uv);

    float ang = atan(uv.y,uv.x);

    // we divide by `r` so that the growth is more spread out in the center
    float growthPortion = 0.2 / pow(r / borderCenter, 3.); // proportion of half the circle, 1 => half the circle is modified

    float growthTime = iTime * 0.5;

    float growth = max((sin(ang + growthTime) - 1.0) / growthPortion + 1.0, 0.0);

    // growth = 0.;
   
    const float power = 1. / bleedAmount;
    const float baseLightingPower = pow(targetWidth, power) * baseMaxLighting;
    
    float lightingPower = baseLightingPower + growth * 0.03;

    {
        const float outerDecay = 10.; // helps prevent the cutoff on canvas borders
        const float outerDecayStart = borderCenter;
        lightingPower *= clamp(outerDecay + 1. - r / outerDecayStart * outerDecay, 0., 1.0);
    }

    float lightingDist = pow(abs(r - borderCenter), power);
    float maxLighting = baseMaxLighting + growth * 1.0;

    float lightMod = min(lightingPower / lightingDist, maxLighting);
    
    color += blue * lightMod;
        
    vec3 lineData = line();
    // lineData.z = 0.;
    if (lineData.z > 0.) {
        // it should be possible to do a ton of different shapes just by transforming lineData here

        // triangle, nice but I think circular will be better
        vec2 dir = -lineData.xy;

        // roughly circular, looks bad on angles/corners, we don't want this
        // vec2 dir = -lineData.xy * (1.- lineData.z);

        // flat top/trapezoid, kinda bad because the tip reflects no light
        // vec2 dir = -lineData.xy;
        // if (lineData.z > 0.66) {
        //     dir = vec2(0.);
        // }


        vec2 lightDir = normalize(-uv);

        float lightPower = 0.5; // global light power, regardless of distance

        lightPower += 2. * (1. - length(dir)) * baseLightingPower; // top down light
        lightPower += max(dot(dir, lightDir),0.) * baseLightingPower / lightingDist * 1.; // distance based with normals

        float growthAngle = pi12 - growthTime;
        vec2 lightPos = borderCenter * vec2(cos(growthAngle),sin(growthAngle));

        float growthLightDist = pow(distance(uv, lightPos), power);

        lightPower += 1. * max(0., dot(normalize(uv - lightPos), dir.xy)) / growthLightDist;
        lightPower += 0.1 / growthLightDist;

        color += blue * lightPower;
    } else {
        if (r < borderCenter) {
            color += hexBgColor() * min(maxLighting - lightMod, 1.);
        }
        color += blue / (-lineData.z + 0.2) * 0.3;
    }

    fragColor = vec4(color, 1.);
}
