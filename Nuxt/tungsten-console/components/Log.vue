<script setup lang="ts">
    import {LogData} from "~/types/LogData";

    const props = defineProps({
        log: Object as () => LogData
    });
</script>

<template>
    <div class="log-container">
        <div class="time"><p>{{ props.log.timeStamp }}</p></div>
        <div class="type"><p>{{ props.log.logType }}</p></div>
        <div class="log">{{ props.log.logString }}</div>
    </div>
</template>

<style scoped>
.log-container {
    width: 100%;
    //height: 25px;
    padding-left: 16px;
    padding-right: 16px;
    display: flex;
    flex-direction: row;
}

.time {
    width: 300px;
    //background-color: rgb(0, 0, 0);
}

.type {
    width: 100px;
    //background-color: rgb(25, 25, 25);
}

.log {
    flex: 1;
}
</style>