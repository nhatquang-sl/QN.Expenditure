qncpdown () {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR
    
    cmd="docker compose --env-file credentials/qex/.env down" # --remove-orphans --volumes"
    echo -e "\e[32m$cmd\e[0m"  # Green color for selected item
    eval $cmd

    cd $curDir
}