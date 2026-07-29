qncpup () {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR
    
    cmd="docker compose --env-file credentials/qex/.env up --build -d"
    echo -e "\e[32m$cmd\e[0m"  # Green color for selected item
    eval $cmd

    cd $curDir
}