<script setup lang="ts">
    import { ref } from "vue"
    import { nextTick } from "vue";
    import {LogData} from "~/types/LogData";

    interface Logs {
        logs: LogData[];
    }

    // routes
    const host = ref<string>("localhost");
    const port = ref<string>("3001");
    const httpUrl = computed(() => {
        return `http://${host.value}:${port.value}`;
    });

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

    // autoscroll
    const autoscroll = ref<boolean>(true);

    onMounted(() => {
        // get the current host and port
        // host.value = window.location.hostname;
        // port.value = window.location.port || port.value;

        // start fetching logs
        intervalId.value = setInterval(() => {
            fetchNewLogs();
        }, intervalTimeout) as unknown as number;
    });

    async function fetchNewLogs() {
        let timeStamp: string = logs.value.length > 0 ? logs.value[logs.value.length - 1].timeStamp : "0";

        let failed: boolean = false;
        let error: string = "";

        let url: string = `${httpUrl.value}${logRoute}?timeStamp=${timeStamp}`;

        await fetch(url, {
            method: "GET",
            headers: {"Content-Type": "application/json"},
        }).then((data: Response) => {
            if (!data.ok) {
                console.error(`Failed to fetch new logs ${data.status}`);
                failed = true;
                error = data.statusText;
                return;
            }

            data.json().then((newLogs: Logs) => {
                if (newLogs.logs.length > 0) {
                    logs.value.push(...newLogs.logs);

                    nextTick(() => {
                        if(autoscroll.value
                        && logContainer.value) {
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
        fetch(`${httpUrl.value}${commandRoute}`, {
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

            <UButton
                class="btn-scroll"
                icon="i-heroicons-arrow-down-20-solid"
                size="sm"
                color="primary"
                square
                :variant="autoscroll ? 'solid' : 'outline'"
                @click="autoscroll = !autoscroll"
            />

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

.input {
    width: 100%;
    padding: 16px;
    display: flex;
    gap: 16px;
    flex-direction: row;
}

.command {
    flex: 1;
}

.search {
    width: 300px;
}

.btn-scroll {
    width: 32px;
    height: 32px;
}

</style>