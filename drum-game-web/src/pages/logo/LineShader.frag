#version 300 es
precision highp float;

in vec3 vPos;
in vec2 vNormal;
out vec4 oFragColor;

void main(void) {        
    if (vPos.z >= 0.) {
        vec3 lineData = vec3(vNormal.xy / 2. + 0.5, vPos.z);
        oFragColor = vec4(lineData, 1.0);
    } else {
        oFragColor = vec4(0.);
    }
}
