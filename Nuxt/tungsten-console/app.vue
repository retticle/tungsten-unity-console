<script setup lang="ts">
    import {ref} from "vue"

    interface WebSocketMessage<T> {
        type: string;
        data: T;
    }

    // todo: do we need this type?
    interface Logs {
        logs: Log[];
    }

    interface Log {
        logString: string;
        stackTrace: string;
        logType: string;
        customColor: boolean;
        // textColor: Color;
        // bgColor: Color;
    }

    // routes
    const port: number = 8181;
    const httpUrl: string = `http://localhost:${port}`;
    const wsUrl: string = `ws://localhost:${port}`;

    const logRoute: string = "/log";
    const commandRoute: string = "/command";

    const input = ref<string>("");

    let socket: WebSocket;

    // logs
    const intervalTimeout: number = 100;
    const intervalId = ref<number>(-1);
    const logs = ref<Log[]>([]);

    onMounted(() => {
        // start fetching logs
        intervalId.value = setInterval(() => {
            fetchNewLogs();
        }, intervalTimeout) as unknown as number;

        // socket = new WebSocket(wsUrl);
        //
        // // connection opened
        // socket.addEventListener("open", function (event) {
        //     console.log("Server connection opened");
        //     socket.send("Hello Server!");
        // });
        //
        // // connection closed
        // socket.addEventListener("close", function (event) {
        //     console.log("Server connection closed: ", event.code);
        // });
        //
        // // listen for messages
        // socket.addEventListener("message", function (event) {
        //     let message = JSON.parse(event.data);
        //
        //     console.log("Message from server: ", event.data);
        //
        //     switch(message.type) {
        //         case "logs_new":
        //             let newLogs: Logs = message.data;
        //             logs.value.push(...newLogs.logs);
        //         break;
        //
        //         default:
        //             console.error(`Unknown message type: ${message.type}`);
        //     }
        // });
        //
        // // listen for errors
        // socket.addEventListener("error", function (event) {
        //     console.log("Error from server: ", event)
        // });
    });

    async function fetchNewLogs() {
        await fetch(`${httpUrl}${logRoute}`, {
            method: "GET",
            headers: {"Content-Type": "application/json"},
        }).then((data: Response) => {
            if (!data.ok) {
                console.error(`Failed to fetch new logs ${data.status}`);
                return;
            }

            data.json().then((newLogs: Logs) => {
                logs.value.push(...newLogs.logs);
            }).catch((error) => {
                // todo display a toast notification with the error
                console.error(`Failed to parse response: ${error}`);
            });
        }).catch((error) => {
            // todo display a toast notification with the error
            console.error(`Failed to fetch new logs: ${error}`);
        });
    }

    function submitCommand() {
        fetch(`${httpUrl}${commandRoute}`, {
            method: "POST",
            headers: {"Content-Type": "application/json"},
            body: JSON.stringify({command: input.value})
        });

        input.value = "";
    }
</script>

<template>
    <div class="console">
        <div class="log">
            <div class="log-entry" v-for="(log, index) in logs" :key="index">
                {{ log.logString }}
            </div>

        </div>

        <div class="input">
            <div class="command">
                <UInput
                    icon="i-heroicons-code-bracket-20-solid"
                    size="sm"
                    color="white"
                    :trailing="false"
                    @keyup.enter="submitCommand"
                    v-model="input"
                />
            </div>
            <div class="search">
                <UInput
                    icon="i-heroicons-magnifying-glass-20-solid"
                    size="sm"
                    color="white"
                    :trailing="false"
                />
            </div>
        </div>
    </div>
</template>

<style scoped>
.console {
    width: 100vw;
    height: 100vh;
    display: flex;
    flex-direction: column;
    margin: 0;
}

.log {
    flex: 1;
    overflow-y: auto;
}

.log-entry {
    padding: 16px;
}

.input {
    width: 100%;
    padding: 16px;
    display: flex;
    flex-direction: row;
}

.command {
    flex: 1;
}

.search {
    margin-left: 16px;
    width: 300px;
}
</style>