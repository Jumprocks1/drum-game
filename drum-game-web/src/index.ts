import { Start } from "./framework/Framework";
import "./main.scss";
import Root from "./Root";

const prod = process.env.NODE_ENV === "production";


Start(Root, {
    baseName: prod ? "drum-game" : undefined
});