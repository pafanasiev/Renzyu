/// <reference path="_references.js" />
function game(token) {
    var self = this;
    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/gameHub")
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    this.connection = connection;
    this.gameId = ko.observable();
    this.token = ko.observable(token);
    this.isPrivateGame = ko.computed(function () {
        return token !== "";
    });
    /*
    state:
    0: initializing
    8: waiting for opponent
    1: my turn
    2: opponent turn
    3: won
    4: lost
    5: waiting for response for rematch
    6: received request for rematch
    7: opponent disconnected
    */
    this.message = ko.observable("");
    this.state = ko.observable();
    this.player = ko.observable("");
    this.opponent = ko.observable("");
    this.board = new board();

    this.requestToPlay = function (playWithComputer) {
        return connection.invoke("Enqueue", {
            token: self.token(),
            isComputerGame: playWithComputer
        }).catch(function (error) {
            console.error(error.toString());
            self.message("unable to join a game");
        });
    };

    this.playWithComputer = function () {
        return self.requestToPlay(true);
    };

    this.changeState = function (newState) {
        switch (newState) {
            case 0:
                this.message("initializing");
                break;
            case 8:
                this.message("waiting for a player to join...");
                break;
            case 3:
                this.message("you Won!");
                break;
            case 4:
                this.message("you Lost :(");
                break;
            case 7:
                this.message("your opponent has disconnected");
                break;
            case 2:
                this.message("waiting for opponent to make a move");
                break;
            case 1:
                this.message("your turn!");
                break;
        }
        this.state(newState);
    };

    this.initialize = function () {
        connection.on("ReceiveMove", function (x, y, mark) {
            console.log("ReceiveMove. mark: " + mark + ", x:" + x + ", y:" + y);
            self.receiveMove(x, y, mark);
        });
        connection.on("ReceiveWin", function (points, mark) {
            console.log("ReceiveWin. mark: " + mark);
            self.receiveWin(points, mark);
        });
        connection.on("ReceiveJoin", function (gameId, myMark) {
            console.log("ReceiveJoin. gameId:" + gameId + ", myMark:" + myMark);
            self.receiveJoin(gameId, myMark);
        });
        connection.on("ReceiveDisconnect", function () {
            console.log("ReceiveDisconnect");
            self.receiveDisconnect();
        });

        self.changeState(8);
        connection.start()
            .then(function () {
                return self.requestToPlay(false);
            })
            .catch(function (error) {
                console.error(error.toString());
                self.message("unable to connect to the game server");
            });
    };

    this.receiveJoin = function (gameId, myMark) {
        this.gameId(gameId);
        this.myMark = ko.observable(myMark);

        // Marks are 1 or 2, which map directly to the initial turn states.
        this.changeState(myMark);
    };

    this.receiveMove = function (x, y, mark) {
        if (this.myMark() === mark) {
            this.changeState(2);
        } else {
            this.changeState(1);
        }
        this.board.setCell(x, y, mark);
    };

    this.receiveWin = function (points, mark) {
        for (var i = 0; i < points.length; i++) {
            var point = points[i];
            this.board.setCell(point.x, point.y, 5);
        }
        this.changeState(this.myMark() === mark ? 3 : 4);
    };

    this.receiveDisconnect = function () {
        this.changeState(7);
    };

    game.current = this;
}

function board() {
    var x = 19;
    var y = 19;
    this.rows = ko.observableArray([]);
    for (var i = 0; i < y; i++) {
        var newRow = new row();
        for (var j = 0; j < x; j++) {
            newRow.cells.push(new cell(j, i));
        }
        this.rows.push(newRow);
    }
}

board.prototype.setCell = function (x, y, value) {
    console.log("setCell. x: " + x + ", y:" + y + ", value: " + value);
    this.rows()[y].cells()[x].mark(value);
};

board.prototype.getCell = function (x, y) {
    return this.rows()[y].cells()[x].value();
};

function row() {
    this.cells = ko.observableArray([]);
}

function cell(x, y) {
    this.x = x;
    this.y = y;
    this.value = ko.observable(0);
    this.mark = function (value) {
        this.value(value);
    };
    this.sendMove = function () {
        if (game.current.state() === 1) {
            game.current.connection.invoke("SendMove", this.x, this.y)
                .catch(function (error) {
                    console.error(error.toString());
                    game.current.message("unable to send the move");
                });
        }
    };
}
