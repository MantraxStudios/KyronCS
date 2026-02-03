function OnStart() {
    console.log("Game JS Started");
    
    this.time = 0;
    this.speed = 2.5;
}

function OnTick(deltaTime) {
    this.time += deltaTime;

    var position = Math.sin(this.time * this.speed);

    if (this.time > 1) {
        console.log("2 segundos pasaron");
        this.time = 0;
    }
}
r