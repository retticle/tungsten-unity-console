<script setup lang="ts">
    import { ref } from "vue"
    import { nextTick } from "vue";
    import {LogData} from "~/types/LogData";

    interface Logs {
        logs: LogData[];
    }

    // const columns = [
    // {
    //     key: "timeStamp",
    //     label: "Time",
    // },
    // {
    //     key: "logType",
    //     label: "Type",
    // },
    // {
    //     key: "logString",
    //     label: "Log",
    // },
    // ];
    
    // routes
    const defaultPort: number = 3001;
    let httpUrl: string = `http://localhost:${defaultPort}`;

    const logRoute: string = "/log";
    const commandRoute: string = "/command";

    // log
    const logContainer = ref<HTMLDivElement | null>(null);

    // input
    const input = ref<string>("");

    // search
    const search = ref<string>("");

    // logs
    const intervalTimeout: number = 1000;
    const intervalId = ref<number>(-1);
    const logs = ref<LogData[]>([]);
    const filteredLogs = computed(() => {
        if (!search.value) {
            return logs.value;
        }

        return logs.value.filter((log) => {
            return log.logString.toLowerCase().includes(search.value.toLowerCase());
        });
    })

    // notifications
    const toast = useToast();

    onMounted(() => {
        // get the current host and port
        const host = window.location.hostname;
        const port = window.location.port || defaultPort;
        // httpUrl = `http://${host}:${port}`;

        // start fetching logs
        intervalId.value = setInterval(() => {
            fetchNewLogs();
        }, intervalTimeout) as unknown as number;
    });

    async function fetchNewLogs() {
        let timeStamp: string = logs.value.length > 0 ? logs.value[logs.value.length - 1].timeStamp : "0";

        await fetch(`${httpUrl}${logRoute}?timeStamp=${timeStamp}`, {
            method: "GET",
            headers: {"Content-Type": "application/json"},
        }).then((data: Response) => {
            if (!data.ok) {
                console.error(`Failed to fetch new logs ${data.status}`);
                return;
            }

            data.json().then((newLogs: Logs) => {
                if (newLogs.logs.length > 0) {
                    logs.value.push(...newLogs.logs);

                    nextTick(() => {
                        if(logContainer.value) {
                        logContainer.value.scrollTop = logContainer.value.scrollHeight;
                    }});
                }
            }).catch((error) => {
                // todo display a toast notification with the error
                console.error(`Failed to parse response: ${error}`);
            });
        }).catch((error) => {
            console.error(`Failed to fetch new logs: ${error}`);
            toast.add({ title: "Failed to fetch new logs", description: error })
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
        <UNotifications />
        <div class="log" ref="logContainer">
<!--            <div class="log-entry" v-for="(log, index) in logs" :key="index">-->
<!--                {{ log.logString }}-->
<!--            </div>-->
<!--            <UTable :columns="columns" :rows="filteredLogs" />-->

            <Log v-for="(log, index) in filteredLogs" :key="index" :log="log" />
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
                    v-model="search"
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