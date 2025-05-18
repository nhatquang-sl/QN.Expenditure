qndkbuildui () {
    local ver=$1
    if [[ ${#ver} -gt 0 ]]; then # Up
        curDir="$(pwd)"
        echo $curDir
        cd $QNEDIR
        
        echo -e "\e[32mdocker build . --no-cache --progress plain -t qex.ui:$ver -f src/WebUI.React/Dockerfile\e[0m"  # Green color for selected item
        eval "docker build . --no-cache --progress plain -t qex.ui:$ver -f src/WebUI.React/Dockerfile"

        cd $curDir
    fi
}